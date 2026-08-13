using Server.Services;
using Server.Services.Contracts;
using Server.Services.Implementation;
using System.Text.Json;

namespace Tests.Unit.Flows;

public sealed class FlowEmulatorServiceTests
{
    [Test]
    public async Task AppliesScheduledInputsOnlyAtScanBoundariesAndCapturesOutputs()
    {
        using var service = new FlowEmulatorService(new Resolver(), new Compiler(), new MachineFactory());
        var created = await service.CreateAsync(Source(), default);

        service.SetInputs(created.EmulatorId, [new EmulatorInputChange("input-01", true, EffectiveAtMilliseconds: 10)]);
        var before = service.Advance(created.EmulatorId, 9, scan: true);
        var after = service.Advance(created.EmulatorId, 1, scan: true);

        Assert.Multiple(() =>
        {
            Assert.That(before.OutputHistory[^1].ProposedValue, Is.False);
            Assert.That(after.OutputHistory[^1].ProposedValue, Is.True);
            Assert.That(after.VirtualTimeMilliseconds, Is.EqualTo(10));
            Assert.That(service.ExportScenario(created.EmulatorId).Inputs, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task ModelsOutputFailureWithoutChangingTheProposedValue()
    {
        using var service = new FlowEmulatorService(new Resolver(), new Compiler(), new MachineFactory());
        var created = await service.CreateAsync(Source(), default);
        service.SetInputs(created.EmulatorId, [new EmulatorInputChange("input-01", true)]);
        service.InjectFault(created.EmulatorId, "output_failure");

        var snapshot = service.Advance(created.EmulatorId, 0, scan: true);

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.OutputHistory[^1].ProposedValue, Is.True);
            Assert.That(snapshot.OutputHistory[^1].EffectiveValue, Is.False);
            Assert.That(snapshot.OutputHistory[^1].Quality, Is.EqualTo("bad"));
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
            var value = inputs.Single().Value;
            return new FlowVmScanResult(
                ++_scan,
                sampledAtMilliseconds,
                [value],
                [new FlowVmCommand("output-01", value)]);
        }

        public void Reset() => _scan = 0;

        public void Dispose()
        {
        }
    }
}
