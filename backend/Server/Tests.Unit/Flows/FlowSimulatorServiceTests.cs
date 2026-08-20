using Server.Common.Contracts;
using Server.Compiler.Contracts;
using Server.Compiler.Services;
using Server.Services;
using Server.Services.Contracts;
using Server.Services.Implementation;

namespace Tests.Unit.Flows;

public sealed class FlowSimulatorServiceTests
{
    [Test]
    public async Task DraftRunsInAnIsolatedShadowSessionAndStopDisposesTheMachine()
    {
        var machines = new MachineFactory();
        var points = new RecordingPointAdapter();
        using var emulators = new FlowEmulatorService(new Resolver(), new Compiler(), machines);
        using var sessions = new FlowSimulatorSessionRegistry(TimeProvider.System);
        var service = CreateService(machines, points, emulators, sessions);

        var started = await service.StartAsync(Source(), false, default);
        var stepped = await service.StepTickAsync(Source().Id, started.SessionId, default);
        await service.StopAsync(Source().Id, started.SessionId, default);

        Assert.Multiple(() =>
        {
            Assert.That(started.SourceDigest, Is.EqualTo("artifact-digest"));
            Assert.That(stepped.Snapshot?.TickNumber, Is.EqualTo(1));
            Assert.That(points.Published, Is.Empty, "simulator output must remain shadow-only");
            Assert.That(machines.LastMachine?.Disposed, Is.True);
        });
    }

    [Test]
    public async Task ReplacementIsPerFlowAndDoesNotUseTheDebugSessionRegistry()
    {
        var machines = new MachineFactory();
        using var emulators = new FlowEmulatorService(new Resolver(), new Compiler(), machines);
        using var sessions = new FlowSimulatorSessionRegistry(TimeProvider.System);
        var ordinaryDebugRegistry = new FlowDebugSessionRegistry();
        var service = CreateService(machines, new RecordingPointAdapter(), emulators, sessions);

        var first = await service.StartAsync(Source(), false, default);
        var second = await service.StartAsync(Source(), true, default);

        Assert.Multiple(() =>
        {
            Assert.That(second.SessionId, Is.Not.EqualTo(first.SessionId));
            Assert.That(ordinaryDebugRegistry.Session, Is.Null);
        });
        ordinaryDebugRegistry.Dispose();
    }

    private static FlowSimulatorService CreateService(
        IFlowVirtualMachineFactory machines,
        IFlowPointAdapter points,
        FlowEmulatorService emulators,
        FlowSimulatorSessionRegistry sessions) => new(
            new Resolver(), new Compiler(), new Transport(), machines, points, emulators, sessions);

    private static ExecutableFlowSource Source() => new()
    {
        Id = "simulated-flow",
        Revision = 7,
        ControllerTemplateId = "controller-a",
        ControllerTemplateRevision = 1,
        Nodes = [new ExecutableFlowNode { Id = "constant", Kind = FlowNodeKind.DigitalConstant }]
    };

    private sealed class Resolver : IFlowCompilationTargetResolver
    {
        public Task<FlowCompilationTarget> ResolveAsync(ExecutableFlowSource source, CancellationToken cancellationToken) =>
            Task.FromResult(new FlowCompilationTarget { ControllerTemplate = null! });
    }

    private sealed class Compiler : IFlowCompiler
    {
        public FlowCompilationResult Compile(FlowCompilationRequest request) => new()
        {
            Artifact = new byte[] { 1 },
            ArtifactSha256 = "artifact-digest",
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
        public Machine? LastMachine { get; private set; }
        public IFlowVirtualMachine Create(ReadOnlyMemory<byte> artifact) => LastMachine = new Machine();
    }

    private sealed class Machine : IFlowVirtualMachine
    {
        private ulong _scan;
        public bool Disposed { get; private set; }
        public FlowVmScanResult Scan(IReadOnlyList<FlowVmInput> inputs, ulong sampledAtMilliseconds) => throw new NotSupportedException();
        public FlowVmExecutionFrame BeginScan(IReadOnlyList<FlowVmInput> inputs, ulong sampledAtMilliseconds) => new(0, FlowOpcode.Commit, true, [], [], [], []);
        public FlowVmExecutionFrame StepInstruction() => throw new NotSupportedException();
        public FlowVmScanResult CommitScan() => new(++_scan, 1, [true], [new FlowVmCommand("output", true)]);
        public void AbortScan() { }
        public void Reset() => _scan = 0;
        public void Dispose() => Disposed = true;
    }

    private sealed class RecordingPointAdapter : IFlowPointAdapter
    {
        public List<IReadOnlyList<FlowVmCommand>> Published { get; } = [];
        public Task<IReadOnlyList<FlowVmInput>> ReadAsync(IReadOnlyList<string> pointIds, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<FlowVmInput>>([]);
        public Task PublishAsync(string flowId, IReadOnlyList<FlowVmCommand> commands, CancellationToken cancellationToken)
        {
            Published.Add(commands);
            return Task.CompletedTask;
        }
    }

    private sealed class Transport : IControllerDebugTransport
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