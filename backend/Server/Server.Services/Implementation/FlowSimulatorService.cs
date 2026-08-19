using Server.Common.Contracts;
using Server.Common.Services;

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
        var emulator = await emulators.CreateAsync(source, cancellationToken);
        try { entry = sessions.Add(source.Id, registry, replaceExisting, emulator.EmulatorId, () => emulators.Delete(emulator.EmulatorId)); }
        catch
        {
            registry.Dispose();
            emulators.Delete(emulator.EmulatorId);
            throw;
        }
        try
        {
            var result = await Debug(registry).StartAsync(new StartFlowDebugSession(source, "emulator", false, emulator.EmulatorId), cancellationToken);
            return Map(result, entry, sessions.Touch(entry));
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
        return Map(entry.Registry.Session!, entry, sessions.Touch(entry));
    }

    public async Task<FlowSimulatorSession> ApplyInputsAndStepAsync(
        string flowId,
        string sessionId,
        IReadOnlyList<EmulatorInputChange> inputs,
        CancellationToken cancellationToken)
    {
        var entry = Require(flowId, sessionId);
        emulators.SetInputs(entry.EmulatorId!, inputs);
        await Debug(entry.Registry).StepAsync(flowId, sessionId, cancellationToken);
        return Map(entry.Registry.Session!, entry, sessions.Touch(entry));
    }

    public Task<FlowSimulatorSession> ApplyInputsAsync(
        string flowId,
        string sessionId,
        IReadOnlyList<EmulatorInputChange> inputs,
        CancellationToken cancellationToken)
    {
        var entry = Require(flowId, sessionId);
        emulators.SetInputs(entry.EmulatorId!, inputs);
        return Task.FromResult(Map(entry.Registry.Session!, entry, sessions.Touch(entry)));
    }

    public async Task<FlowSimulatorSession> AdvanceAsync(string flowId, string sessionId, ulong milliseconds, CancellationToken cancellationToken)
    {
        var entry = Require(flowId, sessionId);
        emulators.Advance(entry.EmulatorId!, milliseconds, scan: false);
        await Debug(entry.Registry).StepAsync(flowId, sessionId, cancellationToken);
        return Map(entry.Registry.Session!, entry, sessions.Touch(entry));
    }

    public Task<FlowSimulatorSession> InjectFaultAsync(string flowId, string sessionId, string? fault, CancellationToken cancellationToken)
    {
        var entry = Require(flowId, sessionId);
        emulators.InjectFault(entry.EmulatorId!, fault);
        return Task.FromResult(Map(entry.Registry.Session!, entry, sessions.Touch(entry)));
    }

    public async Task<FlowSimulatorSession> ResetIoAsync(string flowId, string sessionId, bool powerCycle, CancellationToken cancellationToken)
    {
        var entry = Require(flowId, sessionId);
        emulators.Reset(entry.EmulatorId!, powerCycle);
        var debug = await Debug(entry.Registry).RestartAsync(flowId, sessionId, cancellationToken);
        return Map(debug, entry, sessions.Touch(entry));
    }

    public Task<FlowSimulatorSession> ResetInputsAsync(string flowId, string sessionId, CancellationToken cancellationToken)
    {
        var entry = Require(flowId, sessionId);
        emulators.ResetInputs(entry.EmulatorId!);
        return Task.FromResult(Map(entry.Registry.Session!, entry, sessions.Touch(entry)));
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
        return Map(await operation(Debug(entry.Registry)), entry, sessions.Touch(entry));
    }

    private FlowSimulatorSessionRegistry.Entry Require(string flowId, string sessionId)
    {
        var entry = sessions.Get(flowId) ?? throw new FlowSimulatorException("simulator_session_not_found", "The simulator session was not found or has expired.");
        if (!string.Equals(entry.Registry.Session?.DebugSessionId, sessionId, StringComparison.Ordinal))
        {
            throw new FlowSimulatorException("simulator_session_not_found", "The simulator session was not found.");
        }

        return entry;
    }

    private FlowDebugService Debug(FlowDebugSessionRegistry registry) => new(
        targetResolver, compiler, transport, registry, machines, new ShadowPointAdapter(points), emulators);

    private FlowSimulatorSession Map(FlowDebugSession session, FlowSimulatorSessionRegistry.Entry entry, uint lease) => new()
    {
        SessionId = session.DebugSessionId,
        FlowId = session.FlowId,
        SourceRevision = session.Revision,
        SourceDigest = session.SourceDigest ?? throw new InvalidOperationException("Simulator compilation did not return a digest."),
        LifecycleState = session.LifecycleState == "fault" ? "faulted" : session.LifecycleState,
        Capabilities = session.Capabilities,
        Snapshot = session.Snapshot,
        Io = entry.EmulatorId is null ? null : emulators.Get(entry.EmulatorId),
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