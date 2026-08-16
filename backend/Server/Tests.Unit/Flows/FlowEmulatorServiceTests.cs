using Server.Services;
using Server.Services.Contracts;
using Server.Services.Implementation;
using System.Text.Json;

namespace Tests.Unit.Flows;

public sealed class FlowEmulatorServiceTests
{
    /// <summary>
    /// Purpose: Proves inactive emulator instances expire and release their VM resources.
    /// Description: Advances a controllable clock beyond the shared lease and observes that lookup rejects the expired ID.
    /// </summary>
    [Test]
    public async Task ExpiredInstancesAreRemovedDeterministically()
    {
        // Arrange: Create one emulator against a fixed clock.
        var time = new MutableTimeProvider(DateTimeOffset.Parse("2026-08-15T00:00:00Z"));
        using var service = new FlowEmulatorService(new Resolver(), new Compiler(), new MachineFactory(), time);
        var created = await service.CreateAsync(Source(), default);

        // Act: Cross the lease boundary before looking up the instance.
        time.Advance(FlowEmulatorService.Lease);

        // Assert: Expiry is observable as the normal not-found contract.
        Assert.That(
            () => service.Get(created.EmulatorId),
            Throws.TypeOf<FlowEmulatorNotFoundException>());
    }

    /// <summary>
    /// Purpose: Bounds server memory by enforcing the shared active-emulator limit before allocation.
    /// Description: Fills the registry and verifies one additional instance is rejected with a stable simulator code.
    /// </summary>
    [Test]
    public async Task ActiveInstanceLimitIsEnforced()
    {
        // Arrange: Fill every permitted emulator slot.
        using var service = new FlowEmulatorService(new Resolver(), new Compiler(), new MachineFactory());
        for (var index = 0; index < FlowEmulatorService.MaximumInstances; index++)
        {
            await service.CreateAsync(Source() with { Id = $"flow-{index}" }, default);
        }

        // Act and assert: The next request is rejected before it can remain registered.
        var error = Assert.ThrowsAsync<FlowSimulatorException>(async () =>
            await service.CreateAsync(Source() with { Id = "overflow" }, default));
        Assert.That(error!.Code, Is.EqualTo("simulator_limit_exceeded"));
    }
    [Test]
    public async Task AppliesScheduledInputsOnlyAtScanBoundariesAndCapturesOutputs()
    {
        using var service = new FlowEmulatorService(new Resolver(), new Compiler(), new MachineFactory());
        var created = await service.CreateAsync(Source(), default);

        service.SetInputs(created.EmulatorId, [new EmulatorInputChange("input-01", FlowVmValue.FromBoolean(true), EffectiveAtMilliseconds: 10)]);
        var before = service.Advance(created.EmulatorId, 9, scan: true);
        var after = service.Advance(created.EmulatorId, 1, scan: true);

        Assert.Multiple(() =>
        {
            Assert.That(before.OutputHistory[^1].ProposedValue.Boolean, Is.False);
            Assert.That(after.OutputHistory[^1].ProposedValue.Boolean, Is.True);
            Assert.That(after.VirtualTimeMilliseconds, Is.EqualTo(10));
        });
    }

    [Test]
    public async Task ModelsOutputFailureWithoutChangingTheProposedValue()
    {
        using var service = new FlowEmulatorService(new Resolver(), new Compiler(), new MachineFactory());
        var created = await service.CreateAsync(Source(), default);
        service.SetInputs(created.EmulatorId, [new EmulatorInputChange("input-01", FlowVmValue.FromBoolean(true))]);
        service.InjectFault(created.EmulatorId, "output_failure");

        var snapshot = service.Advance(created.EmulatorId, 0, scan: true);

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.OutputHistory[^1].ProposedValue.Boolean, Is.True);
            Assert.That(snapshot.OutputHistory[^1].EffectiveValue.Quality, Is.EqualTo(DataQuality.Bad));
            Assert.That(snapshot.OutputHistory[^1].Quality, Is.EqualTo(DataQuality.Bad));
        });
    }

    /// <summary>
    /// Purpose: Protects typed interface inputs as one coherent simulator scan image.
    /// Description: Applies a numeric terminal value and quality through the atomic operation, then verifies the committed interface output and reset default.
    /// </summary>
    [Test]
    public async Task AppliesTypedInterfaceInputsAtomicallyAndResetRestoresDefaults()
    {
        using var service = new FlowEmulatorService(new Resolver(), new Compiler(), new MachineFactory());
        var source = Source() with
        {
            Interface = new FlowInterface
            {
                Inputs = [new FlowInterfaceInput { Id = "temperature", Name = "Temperature", DataType = DataType.Number, Units = "°C", DefaultValue = JsonSerializer.SerializeToElement(12.5), Required = true }],
                Outputs = [new FlowInterfaceOutput { Id = "result", Name = "Result", DataType = DataType.Number, Units = "°C" }]
            },
            Nodes = [new ExecutableFlowNode { Id = "input", Kind = "flowInput", Configuration = new Dictionary<string, JsonElement> { ["interfaceId"] = JsonSerializer.SerializeToElement("temperature") } }]
        };
        var created = await service.CreateAsync(source, default);

        var stepped = service.ApplyInputsAndStep(created.EmulatorId,
            [new EmulatorInputChange("temperature", FlowVmValue.FromNumber(21.5, DataQuality.Uncertain))]);
        var reset = service.Reset(created.EmulatorId, powerCycle: false);

        Assert.Multiple(() =>
        {
            // Expected outcome: The applied number and quality reach the committed interface output in one scan.
            // Acceptance criteria: The latest sample is numeric 21.5 with interface identity and units, proving typed metadata survived the VM boundary.
            Assert.That(stepped.OutputHistory[^1].OutputId, Is.EqualTo("result"));
            Assert.That(stepped.OutputHistory[^1].IsInterface, Is.True);
            Assert.That(stepped.OutputHistory[^1].Units, Is.EqualTo("°C"));
            Assert.That(stepped.OutputHistory[^1].EffectiveValue.Number, Is.EqualTo(21.5));
            // Expected outcome: Reset restores the persisted default instead of silently substituting zero.
            // Acceptance criteria: The current input equals 12.5 after reset because that is the declared interface default.
            Assert.That(reset.Inputs.Single().TypedValue.Number, Is.EqualTo(12.5));
        });
    }

    private static ExecutableFlowSource Source() => new()
    {
        Id = "flow-a",
        Revision = 1,
        ControllerTemplateId = "controller-a",
        ControllerTemplateRevision = 1,
        Nodes =
        [
            new ExecutableFlowNode
            {
                Id = "input",
                Kind = "digitalInput",
                Configuration = new Dictionary<string, JsonElement>
                {
                    ["pointId"] = JsonSerializer.SerializeToElement("input-01")
                }
            }
        ]
    };

    private sealed class Resolver : IFlowCompilationTargetResolver
    {
        public Task<FlowCompilationTarget> ResolveAsync(
            ExecutableFlowSource source,
            CancellationToken cancellationToken) => Task.FromResult(new FlowCompilationTarget
            {
                ControllerTemplate = null!
            });
    }

    private sealed class Compiler : IFlowCompiler
    {
        public FlowCompilationResult Compile(FlowCompilationRequest request) => new()
        {
            Artifact = new byte[] { 1 },
            ArtifactSha256 = "fixture",
            FlowRevision = request.Source.Revision,
            ControllerTemplateId = request.Source.ControllerTemplateId,
            ControllerTemplateRevision = 1
        };
    }

    private sealed class MachineFactory : IFlowVirtualMachineFactory
    {
        public IFlowVirtualMachine Create(ReadOnlyMemory<byte> artifact) => new Machine();
    }

    private sealed class Machine : IFlowVirtualMachine
    {
        private ulong _scan;

        public FlowVmScanResult Scan(IReadOnlyList<FlowVmInput> inputs, ulong sampledAtMilliseconds)
        {
            var input = inputs.Single();
            var value = input.Value;
            return new FlowVmScanResult(
                ++_scan,
                sampledAtMilliseconds,
                [input.TypedValue],
                [input.IsInterface
                    ? new FlowVmCommand("result", input.TypedValue, isInterface: true)
                    : new FlowVmCommand("output-01", value)]);
        }

        public void Reset() => _scan = 0;

        public void Dispose()
        {
        }
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
    }
}
