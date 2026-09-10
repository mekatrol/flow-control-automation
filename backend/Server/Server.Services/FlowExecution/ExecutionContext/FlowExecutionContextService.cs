#pragma warning disable IDE0011, CC0001, CC0002, CC0003
using Server.Compiler.Services;
using Server.Services.FlowExecution.Debugging;
using Server.Services.FlowExecution.Deployment;
using Server.Services.FlowExecution.Emulation;
using Server.Services.FlowExecution.Simulation;

namespace Server.Services.FlowExecution.ExecutionContext;

internal sealed class FlowExecutionContextService(
    IFlowService flows,
    IFlowCompilationTargetResolver targetResolver,
    IFlowCompiler compiler,
    IControllerDebugTransport transport,
    IFlowVirtualMachineFactory machines,
    IFlowPointAdapter points,
    FlowEmulatorService emulators,
    FlowExecutionContextRegistry contexts,
    TimeProvider timeProvider) : IFlowExecutionContextService
{
    public async Task<FlowExecutionContext> CreateAsync(CreateFlowExecutionContext request, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(request);
        var flow = await flows.GetAsync(request.FlowId, token);
        if (flow.Revision < 0 || checked((uint)flow.Revision) != request.ExpectedRevision)
            throw new FlowExecutionContextConflictException("The saved flow revision does not match expectedRevision.");
        if (string.IsNullOrWhiteSpace(request.TargetId))
            throw new FlowExecutionCapabilityException("target");

        var source = FlowDeploymentService.ToExecutableSource(flow);
        var id = Guid.NewGuid().ToString("N");
        FlowExecutionContextRegistry.Entry? entry = null;
        try
        {
            if (request.Mode == FlowExecutionMode.Simulator)
            {
                if (!string.Equals(request.TargetId, "server", StringComparison.Ordinal))
                    throw new FlowExecutionCapabilityException("simulator target");
                var debugRegistry = new FlowDebugSessionRegistry();
                var simulatorRegistry = new FlowSimulatorSessionRegistry(timeProvider, TimeSpan.FromMinutes(15));
                var simulator = new FlowSimulatorService(targetResolver, compiler, transport, machines, points, emulators, simulatorRegistry);
                var session = await simulator.StartAsync(source, true, token);
                if (request.Breakpoints.Count != 0)
                    session = await simulator.ReplaceBreakpointsAsync(flow.Id, session.SessionId, request.Breakpoints, token);
                entry = NewEntry(id, flow, request, session.SessionId, null, simulator, Map(session, id, checked((uint)flow.Revision)));
            }
            else
            {
                var host = request.TargetId == "server" ? "server" : "controller";
                var registry = new FlowDebugSessionRegistry();
                var debug = new FlowDebugService(targetResolver, compiler, transport, registry, machines, points, emulators);
                var session = await debug.StartAsync(new StartFlowDebugSession(source, host, true), token);
                if (request.Breakpoints.Count != 0)
                    session = await debug.ReplaceBreakpointsAsync(flow.Id, session.DebugSessionId, request.Breakpoints, token);
                entry = NewEntry(id, flow, request, session.DebugSessionId, debug, null, Map(session, id, checked((uint)flow.Revision), request.TargetId));
            }
            contexts.Add(entry, request.ReplaceExisting);
            return Touch(entry);
        }
        catch
        {
            if (entry is not null) await Cleanup(entry, CancellationToken.None);
            throw;
        }
    }

    public Task<FlowExecutionContext> GetAsync(string id, CancellationToken token) => Execute(id, token, async entry =>
        entry.Mode == FlowExecutionMode.Simulator
            ? Map(await entry.Simulator!.GetAsync(entry.FlowId, entry.SessionId, token), entry.Id, entry.Revision)
            : Map(await entry.Debug!.GetAsync(entry.FlowId, entry.SessionId, token), entry.Id, entry.Revision, entry.TargetId));
    public Task<FlowExecutionContext> RunAsync(string id, RunFlowExecution request, CancellationToken token) => Execute(id, token, async entry =>
        entry.Mode == FlowExecutionMode.Simulator
            ? Map(await entry.Simulator!.RunAsync(entry.FlowId, entry.SessionId, request.IntervalMilliseconds, token), entry.Id, entry.Revision)
            : Map(await entry.Debug!.RunAsync(entry.FlowId, entry.SessionId, request.IntervalMilliseconds, token), entry.Id, entry.Revision, entry.TargetId));
    public Task<FlowExecutionContext> PauseAsync(string id, CancellationToken token) => Both(id, token, (s, e) => s.PauseAsync(e.FlowId, e.SessionId, token), (d, e) => d.PauseAsync(e.FlowId, e.SessionId, token));
    public Task<FlowExecutionContext> RestartAsync(string id, CancellationToken token) => Both(id, token, (s, e) => s.RestartAsync(e.FlowId, e.SessionId, token), (d, e) => d.RestartAsync(e.FlowId, e.SessionId, token));
    public Task<FlowExecutionContext> StepTickAsync(string id, CancellationToken token) => Both(id, token, (s, e) => s.StepTickAsync(e.FlowId, e.SessionId, token), async (d, e) => { await d.StepAsync(e.FlowId, e.SessionId, token); return await d.GetAsync(e.FlowId, e.SessionId, token); });
    public Task<FlowExecutionContext> StepNodeAsync(string id, CancellationToken token) => Both(id, token, (s, e) => s.StepNodeAsync(e.FlowId, e.SessionId, token), (d, e) => d.StepNodeAsync(e.FlowId, e.SessionId, token));
    public Task<FlowExecutionContext> StepInstructionAsync(string id, CancellationToken token) => Both(id, token, (s, e) => s.StepInstructionAsync(e.FlowId, e.SessionId, token), (d, e) => d.StepInstructionAsync(e.FlowId, e.SessionId, token));
    public Task<FlowExecutionContext> RunToAsync(string id, FlowDebugBreakpoint boundary, CancellationToken token) => Both(id, token, (s, e) => s.RunToAsync(e.FlowId, e.SessionId, boundary, token), (d, e) => d.RunToAsync(e.FlowId, e.SessionId, boundary, token));
    public Task<FlowExecutionContext> ReplaceBreakpointsAsync(string id, IReadOnlyList<FlowDebugBreakpoint> values, CancellationToken token) => Both(id, token, (s, e) => s.ReplaceBreakpointsAsync(e.FlowId, e.SessionId, values, token), (d, e) => d.ReplaceBreakpointsAsync(e.FlowId, e.SessionId, values, token));

    public Task<FlowExecutionContext> ApplyInputsAsync(string id, ApplyFlowExecutionInputs request, CancellationToken token) => Simulator(id, token, "edit inputs", (s, e) => s.ApplyInputsAsync(e.FlowId, e.SessionId, request.Inputs, token));
    public Task<FlowExecutionContext> AdvanceAsync(string id, AdvanceFlowExecution request, CancellationToken token) => Simulator(id, token, "advance virtual time", (s, e) => s.AdvanceAsync(e.FlowId, e.SessionId, request.Milliseconds, token));
    public Task<FlowExecutionContext> InjectFaultAsync(string id, InjectFlowExecutionFault request, CancellationToken token) => Simulator(id, token, "inject faults", (s, e) => s.InjectFaultAsync(e.FlowId, e.SessionId, request.Fault, token));
    public Task<FlowExecutionContext> ResetIoAsync(string id, ResetFlowExecutionIo request, CancellationToken token) => Simulator(id, token, "reset I/O", (s, e) => s.ResetIoAsync(e.FlowId, e.SessionId, request.PowerCycle, token));
    public Task<FlowExecutionContext> ResetInputsAsync(string id, CancellationToken token) => Simulator(id, token, "reset inputs", (s, e) => s.ResetInputsAsync(e.FlowId, e.SessionId, token));
    public Task<FlowExecutionContext> EnableLiveOutputAsync(string id, EnableFlowExecutionLiveOutput request, CancellationToken token) => Execute(id, token, async entry =>
    {
        if (!entry.Context.Capabilities.CanEnableLiveOutputs) throw new FlowExecutionCapabilityException("live output");
        return Map(await entry.Debug!.EnableLiveOutputAsync(entry.FlowId, entry.SessionId, request.PointIds, token), entry.Id, entry.Revision, entry.TargetId);
    });
    public Task<FlowExecutionContext> KeepAliveAsync(string id, CancellationToken token) => Execute(id, token, async entry =>
    {
        if (entry.Simulator is not null) await entry.Simulator.KeepAliveAsync(entry.FlowId, entry.SessionId, token);
        return entry.Context;
    });

    public async Task<FlowExecutionContext> StopAsync(string id, CancellationToken token)
    {
        var entry = contexts.Get(id);
        await entry.Gate.WaitAsync(token);
        try
        {
            if (entry.Context.Lifecycle != FlowExecutionLifecycle.Stopped) await Cleanup(entry, token);
            entry.Context = entry.Context with { Lifecycle = FlowExecutionLifecycle.Stopped, LeaseRemainingMilliseconds = 0 };
            return entry.Context;
        }
        finally { entry.Gate.Release(); }
    }

    private Task<FlowExecutionContext> Simulator(string id, CancellationToken token, string capability, Func<IFlowSimulatorService, FlowExecutionContextRegistry.Entry, Task<FlowSimulatorSession>> action) => Execute(id, token, async entry =>
    {
        if (entry.Simulator is null) throw new FlowExecutionCapabilityException(capability);
        return Map(await action(entry.Simulator, entry), entry.Id, entry.Revision);
    });
    private Task<FlowExecutionContext> Both(string id, CancellationToken token, Func<IFlowSimulatorService, FlowExecutionContextRegistry.Entry, Task<FlowSimulatorSession>> simulator, Func<IFlowDebugService, FlowExecutionContextRegistry.Entry, Task<FlowDebugSession>> debug) => Execute(id, token, async entry =>
        entry.Simulator is not null ? Map(await simulator(entry.Simulator, entry), entry.Id, entry.Revision) : Map(await debug(entry.Debug!, entry), entry.Id, entry.Revision, entry.TargetId));
    private async Task<FlowExecutionContext> Execute(string id, CancellationToken token, Func<FlowExecutionContextRegistry.Entry, Task<FlowExecutionContext>> action)
    {
        var entry = contexts.Get(id);
        await entry.Gate.WaitAsync(token);
        try
        {
            if (entry.Context.Lifecycle == FlowExecutionLifecycle.Stopped) return entry.Context;
            entry.Context = await action(entry);
            return Touch(entry);
        }
        finally { entry.Gate.Release(); }
    }

    private FlowExecutionContext Touch(FlowExecutionContextRegistry.Entry entry) => entry.Context = entry.Context with { LeaseRemainingMilliseconds = contexts.Remaining(entry) };
    private FlowExecutionContextRegistry.Entry NewEntry(string id, Flow flow, CreateFlowExecutionContext request, string sessionId, IFlowDebugService? debug, IFlowSimulatorService? simulator, FlowExecutionContext context) => new()
    { Id = id, FlowId = flow.Id, Revision = checked((uint)flow.Revision), Mode = request.Mode, TargetId = request.TargetId, SessionId = sessionId, Debug = debug, Simulator = simulator, Context = context, LastAccess = timeProvider.GetUtcNow() };
    private static async Task Cleanup(FlowExecutionContextRegistry.Entry entry, CancellationToken token)
    {
        if (entry.Simulator is not null) await entry.Simulator.StopAsync(entry.FlowId, entry.SessionId, token);
        else await entry.Debug!.StopAsync(entry.FlowId, entry.SessionId, token);
    }

    private static FlowExecutionContext Map(FlowSimulatorSession value, string id, uint revision) => new()
    {
        Id = id,
        FlowId = value.FlowId,
        Revision = revision,
        Mode = FlowExecutionMode.Simulator,
        Lifecycle = Lifecycle(value.LifecycleState),
        Capabilities = FlowExecutionCapabilities.Simulator,
        Breakpoints = value.Breakpoints,
        Snapshot = value.Snapshot,
        Inspection = value.Inspection,
        Io = value.Io is null ? null : new FlowExecutionIo { Inputs = value.Io.Inputs, OutputHistory = value.Io.OutputHistory, VirtualTimeMilliseconds = value.Io.VirtualTimeMilliseconds, ActiveFault = value.Io.ActiveFault },
        Presentation = new FlowExecutionPresentation { ModeLabel = "Simulator", HostLabel = "Server emulator", IsSimulated = true },
        LeaseRemainingMilliseconds = value.LeaseRemainingMilliseconds
    };
    private static FlowExecutionContext Map(FlowDebugSession value, string id, uint revision, string target) => new()
    {
        Id = id,
        FlowId = value.FlowId,
        Revision = revision,
        Mode = FlowExecutionMode.Debugger,
        Lifecycle = Lifecycle(value.LifecycleState),
        Capabilities = target == "server" ? FlowExecutionCapabilities.ServerDebugger : FlowExecutionCapabilities.ControllerDebugger(true),
        Breakpoints = value.Breakpoints,
        Snapshot = value.Snapshot,
        Inspection = value.Inspection,
        Io = new FlowExecutionIo { LiveOutputEnabled = value.LiveOutputEnabled, LiveOutputPointIds = value.AffectedOutputPoints },
        Presentation = new FlowExecutionPresentation { ModeLabel = "Debugger", HostLabel = target == "server" ? "Server" : target, UsesPhysicalIo = target != "server" },
        LeaseRemainingMilliseconds = value.LeaseRemainingMilliseconds
    };
    private static FlowExecutionLifecycle Lifecycle(string value) => value switch { "ready" => FlowExecutionLifecycle.Ready, "running" => FlowExecutionLifecycle.Running, "paused" => FlowExecutionLifecycle.Paused, "stepping" => FlowExecutionLifecycle.Stepping, "fault" or "faulted" => FlowExecutionLifecycle.Faulted, "stale" => FlowExecutionLifecycle.Stale, "stopped" => FlowExecutionLifecycle.Stopped, _ => FlowExecutionLifecycle.Ready };
}