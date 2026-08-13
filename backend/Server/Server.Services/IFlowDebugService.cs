using Server.Services.Contracts;

namespace Server.Services;

public interface IFlowDebugService
{
    Task<FlowDebugSession> StartAsync(StartFlowDebugSession request, CancellationToken cancellationToken);

    Task<FlowDebugSession> StartAsync(
        ExecutableFlowSource source,
        bool replaceExisting,
        CancellationToken cancellationToken);

    Task<FlowDebugSession> GetAsync(
        string flowId,
        string sessionId,
        CancellationToken cancellationToken);

    Task<DebugRuntimeSnapshot> StepAsync(
        string flowId,
        string sessionId,
        CancellationToken cancellationToken);

    Task<FlowDebugSession> StepInstructionAsync(string flowId, string sessionId, CancellationToken cancellationToken);

    Task<FlowDebugSession> StepNodeAsync(string flowId, string sessionId, CancellationToken cancellationToken);

    Task<FlowDebugSession> RunToAsync(string flowId, string sessionId, FlowDebugBreakpoint breakpoint, CancellationToken cancellationToken);

    Task<FlowDebugSession> ReplaceBreakpointsAsync(
        string flowId,
        string sessionId,
        IReadOnlyList<FlowDebugBreakpoint> breakpoints,
        CancellationToken cancellationToken);

    Task<FlowDebugInspection> InspectAsync(string flowId, string sessionId, CancellationToken cancellationToken);

    Task<FlowDebugSession> RestartAsync(string flowId, string sessionId, CancellationToken cancellationToken);

    Task<FlowDebugSession> RunAsync(string flowId, string sessionId, uint intervalMilliseconds, CancellationToken cancellationToken);

    Task<FlowDebugSession> PauseAsync(string flowId, string sessionId, CancellationToken cancellationToken);

    Task<FlowDebugSession> EnableLiveOutputAsync(
        string flowId,
        string sessionId,
        IReadOnlyList<string> confirmedPointIds,
        CancellationToken cancellationToken);

    Task StopAsync(
        string flowId,
        string sessionId,
        CancellationToken cancellationToken);
}
