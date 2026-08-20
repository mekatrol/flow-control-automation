using Server.Common.Contracts;
using Server.Compiler.Contracts;
using Server.Compiler.Services;
using Server.Services;
using Server.Services.Contracts;
using Server.Services.Implementation;

namespace Tests.Unit.Flows;

public sealed class LocalFlowDebuggerTests
{
    [Test]
    public async Task InstructionPauseInspectsPrivateFrameAndCommitsOnlyOnStepTick()
    {
        var machines = new MachineFactory();
        var emulators = new FlowEmulatorService(new Resolver(), new Compiler(), machines);
        var points = new PointAdapter();
        var service = new FlowDebugService(
            new Resolver(),
            new Compiler(),
            new ControllerTransport(),
            new FlowDebugSessionRegistry(),
            machines,
            points,
            emulators);
        var source = Source();
        var started = await service.StartAsync(new StartFlowDebugSession(source, "server", false), default);

        var paused = await service.StepInstructionAsync(source.Id, started.DebugSessionId, default);
        var inspection = await service.InspectAsync(source.Id, started.DebugSessionId, default);

        Assert.Multiple(() =>
        {
            Assert.That(paused.LifecycleState, Is.EqualTo("paused"));
            Assert.That(inspection.Slots[0].Value, Is.True);
            Assert.That(inspection.NodeValues["constant"].Value, Is.True);
            Assert.That(points.Published, Is.Empty);
        });

        await service.StepAsync(source.Id, started.DebugSessionId, default);
        Assert.That(points.Published, Has.Count.EqualTo(1));
        await service.StopAsync(source.Id, started.DebugSessionId, default);
        emulators.Dispose();
    }

    private static ExecutableFlowSource Source() => new()
    {
        Id = "flow-a",
        Revision = 1,
        ControllerTemplateId = "controller-a",
        ControllerTemplateRevision = 1,
        Nodes = [new ExecutableFlowNode { Id = "constant", Kind = FlowNodeKind.DigitalConstant }]
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
            ControllerTemplateRevision = 1,
            Schedule = ["constant"],
            NodeIndices = new Dictionary<string, ushort> { ["constant"] = 0 }
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
        private bool _stepped;
        private ulong _scan;

        public FlowVmScanResult Scan(IReadOnlyList<FlowVmInput> inputs, ulong sampledAtMilliseconds) =>
            throw new NotSupportedException();

        public FlowVmExecutionFrame BeginScan(IReadOnlyList<FlowVmInput> inputs, ulong sampledAtMilliseconds)
        {
            _stepped = false;
            return Frame();
        }

        public FlowVmExecutionFrame StepInstruction()
        {
            _stepped = true;
            return Frame();
        }

        public FlowVmScanResult CommitScan()
        {
            return new FlowVmScanResult(++_scan, 0, [true], [new FlowVmCommand("output-01", true)]);
        }

        public void AbortScan()
        {
        }

        public void Reset()
        {
            _scan = 0;
        }

        public void Dispose()
        {
        }

        private FlowVmExecutionFrame Frame() => new(
            _stepped ? (ushort)1 : (ushort)0,
            _stepped ? FlowOpcode.Commit : FlowOpcode.PointInput,
            _stepped,
            [_stepped],
            [],
            [],
            []);
    }

    private sealed class PointAdapter : IFlowPointAdapter
    {
        public List<IReadOnlyList<FlowVmCommand>> Published { get; } = [];

        public Task<IReadOnlyList<FlowVmInput>> ReadAsync(
            IReadOnlyList<string> pointIds,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<FlowVmInput>>([]);

        public Task PublishAsync(
            string flowId,
            IReadOnlyList<FlowVmCommand> commands,
            CancellationToken cancellationToken)
        {
            Published.Add(commands);
            return Task.CompletedTask;
        }
    }

    private sealed class ControllerTransport : IControllerDebugTransport
    {
        public Task<ControllerDebugLoadResult> LoadAsync(ReadOnlyMemory<byte> artifact, bool replaceExisting, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ControllerDebugWireStatus> PrepareAsync(ulong sessionId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ControllerDebugWireStatus> GetStatusAsync(ulong sessionId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ControllerDebugSnapshotEnvelope> StepAsync(ulong sessionId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task RenewLeaseAsync(ulong sessionId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ControllerDebugWireStatus> RunAsync(ulong sessionId, uint intervalMilliseconds, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ControllerDebugWireStatus> PauseAsync(ulong sessionId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ControllerDebugLiveOutputResult> EnableLiveOutputAsync(ulong sessionId, IReadOnlyList<string> confirmedPointIds, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ControllerDebugSnapshotEnvelope> ReadSnapshotAsync(ulong sessionId, ulong tickNumber, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task StopAsync(ulong sessionId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}