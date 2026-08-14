using Server.Services.Contracts;

namespace Server.Services.Implementation;

public sealed class FlowSimulatorService(
    IFlowCompilationTargetResolver targetResolver,
    IFlowCompiler compiler,
    IControllerDebugTransport transport,
    IFlowVirtualMachineFactory machines,
    IFlowPointAdapter points,
    FlowEmulatorService emulators,
    FlowSimulatorSessionRegistry sessions) : IFlowSimulatorService
{
    public async Task<FlowSimulatorSession> StartAsync(ExecutableFlowSource source, bool replaceExisting, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        var registry = new FlowDebugSessionRegistry();
        FlowSimulatorSessionRegistry.Entry entry;
        try { entry = sessions.Add(source.Id, registry, replaceExisting); }
        catch { registry.Dispose(); throw; }
        try
        {
            var result = await Debug(registry).StartAsync(new StartFlowDebugSession(source, "server", false), cancellationToken);
            return Map(result, sessions.Touch(entry));
        }
        catch
        {
            sessions.Remove(source.Id, entry);
            throw;
        }
    }

    public async Task<FlowSimulatorSession> GetAsync(string flowId, string sessionId, CancellationToken cancellationToken) =>
        await Execute(flowId, sessionId, (debug) => debug.GetAsync(flowId, sessionId, cancellationToken));

    public async Task<FlowSimulatorSession> StepTickAsync(string flowId, string sessionId, CancellationToken cancellationToken)
    {
        var entry = Require(flowId, sessionId);
        await Debug(entry.Registry).StepAsync(flowId, sessionId, cancellationToken);
        return Map(entry.Registry.Session!, sessions.Touch(entry));
    }

    public async Task<FlowSimulatorSession> StepNodeAsync(string flowId, string sessionId, CancellationToken cancellationToken) =>
        await Execute(flowId, sessionId, (debug) => debug.StepNodeAsync(flowId, sessionId, cancellationToken));
    public async Task<FlowSimulatorSession> StepInstructionAsync(string flowId, string sessionId, CancellationToken cancellationToken) =>
        await Execute(flowId, sessionId, (debug) => debug.StepInstructionAsync(flowId, sessionId, cancellationToken));
    public async Task<FlowSimulatorSession> RestartAsync(string flowId, string sessionId, CancellationToken cancellationToken) =>
        await Execute(flowId, sessionId, (debug) => debug.RestartAsync(flowId, sessionId, cancellationToken));
    public async Task<FlowSimulatorSession> RunAsync(string flowId, string sessionId, uint intervalMilliseconds, CancellationToken cancellationToken) =>
        await Execute(flowId, sessionId, (debug) => debug.RunAsync(flowId, sessionId, intervalMilliseconds, cancellationToken));
    public async Task<FlowSimulatorSession> PauseAsync(string flowId, string sessionId, CancellationToken cancellationToken) =>
        await Execute(flowId, sessionId, (debug) => debug.PauseAsync(flowId, sessionId, cancellationToken));

    public async Task StopAsync(string flowId, string sessionId, CancellationToken cancellationToken)
    {
        var entry = Require(flowId, sessionId);
        try { await Debug(entry.Registry).StopAsync(flowId, sessionId, cancellationToken); }
        finally { sessions.Remove(flowId, sessionId); }
    }

    private async Task<FlowSimulatorSession> Execute(string flowId, string sessionId, Func<IFlowDebugService, Task<FlowDebugSession>> operation)
    {
        var entry = Require(flowId, sessionId);
        return Map(await operation(Debug(entry.Registry)), sessions.Touch(entry));
    }

    private FlowSimulatorSessionRegistry.Entry Require(string flowId, string sessionId)
    {
        var entry = sessions.Get(flowId);
        if (entry is null) throw new FlowSimulatorException("simulator_session_not_found", "The simulator session was not found or has expired.");
        if (!string.Equals(entry.Registry.Session?.DebugSessionId, sessionId, StringComparison.Ordinal))
            throw new FlowSimulatorException("simulator_session_not_found", "The simulator session was not found.");
        return entry;
    }

    private FlowDebugService Debug(FlowDebugSessionRegistry registry) => new(
        targetResolver, compiler, transport, registry, machines, new ShadowPointAdapter(points), emulators);

    private static FlowSimulatorSession Map(FlowDebugSession session, uint lease) => new()
    {
        SessionId = session.DebugSessionId,
        FlowId = session.FlowId,
        SourceRevision = session.Revision,
        SourceDigest = session.SourceDigest ?? throw new InvalidOperationException("Simulator compilation did not return a digest."),
        LifecycleState = session.LifecycleState == "fault" ? "faulted" : session.LifecycleState,
        Capabilities = session.Capabilities,
        Snapshot = session.Snapshot,
        Inspection = session.Inspection,
        Breakpoints = session.Breakpoints,
        LeaseRemainingMilliseconds = lease
    };

    private sealed class ShadowPointAdapter(IFlowPointAdapter inner) : IFlowPointAdapter
    {
        public Task<IReadOnlyList<FlowVmInput>> ReadAsync(IReadOnlyList<string> pointIds, CancellationToken cancellationToken) =>
            inner.ReadAsync(pointIds, cancellationToken);
        public Task PublishAsync(string flowId, IReadOnlyList<FlowVmCommand> commands, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
