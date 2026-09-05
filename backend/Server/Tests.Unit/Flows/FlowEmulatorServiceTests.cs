using Server.Common.Contracts;
using Server.Common.Models;
using Server.Compiler.Contracts;
using Server.Compiler.Services;
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

    [Test]
    public async Task InitializesAndAcceptsAnalogInputs()
    {
        using var service = new FlowEmulatorService(new Resolver(), new Compiler(), new MachineFactory());
        var source = Source() with
        {
            Nodes =
            [
                Source().Nodes[0] with { Kind = FlowNodeKind.AnalogInput }
            ]
        };
        var created = await service.CreateAsync(source, default);

        var updated = service.ApplyInputsAndStep(created.EmulatorId,
            [new EmulatorInputChange("input-01", FlowVmValue.FromNumber(21.5))]);

        Assert.Multiple(() =>
        {
            Assert.That(created.Inputs.Single().TypedValue.DataType, Is.EqualTo(DataType.Number));
            Assert.That(updated.Inputs.Single().TypedValue.Number, Is.EqualTo(21.5));
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
                Kind = FlowNodeKind.DigitalInput,
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

        public void WriteBinary(FlowCompilationResult compilation, string path)
        {
            throw new NotImplementedException();
        }

        public void WriteIntelHex(FlowCompilationResult compilation, string path, uint baseAddress = 0, int bytesPerRecord = 16)
        {
            throw new NotImplementedException();
        }
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
            return new FlowVmScanResult(
                ++_scan,
                sampledAtMilliseconds,
                [input.TypedValue],
                [new FlowVmCommand("output-01", input.TypedValue)]);
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