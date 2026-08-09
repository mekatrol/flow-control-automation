using Server.Services.Contracts;

namespace Server.Services;

public interface IFlowDebugService
{
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

    Task<FlowDebugSession> RunAsync(string flowId, string sessionId, uint intervalMilliseconds, CancellationToken cancellationToken);

    Task<FlowDebugSession> PauseAsync(string flowId, string sessionId, CancellationToken cancellationToken);

    Task StopAsync(
        string flowId,
        string sessionId,
        CancellationToken cancellationToken);
}
