using Server.Services.Contracts;

namespace Server.Services;

public interface IFlowSimulatorService
{
    Task<FlowSimulatorSession> StartAsync(ExecutableFlowSource source, bool replaceExisting, CancellationToken cancellationToken);
    Task<FlowSimulatorSession> GetAsync(string flowId, string sessionId, CancellationToken cancellationToken);
    Task<FlowSimulatorSession> StepTickAsync(string flowId, string sessionId, CancellationToken cancellationToken);
    Task<FlowSimulatorSession> StepNodeAsync(string flowId, string sessionId, CancellationToken cancellationToken);
    Task<FlowSimulatorSession> StepInstructionAsync(string flowId, string sessionId, CancellationToken cancellationToken);
    Task<FlowSimulatorSession> RestartAsync(string flowId, string sessionId, CancellationToken cancellationToken);
    Task<FlowSimulatorSession> RunAsync(string flowId, string sessionId, uint intervalMilliseconds, CancellationToken cancellationToken);
    Task<FlowSimulatorSession> PauseAsync(string flowId, string sessionId, CancellationToken cancellationToken);
    Task StopAsync(string flowId, string sessionId, CancellationToken cancellationToken);
}
