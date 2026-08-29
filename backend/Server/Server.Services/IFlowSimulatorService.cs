using Server.Common.Contracts;

namespace Server.Services;

public interface IFlowSimulatorService
{
    Task<FlowSimulatorSession> StartAsync(ExecutableFlowSource source, bool replaceExisting, CancellationToken cancellationToken);
    Task<FlowSimulatorSession> GetAsync(string flowId, string sessionId, CancellationToken cancellationToken);
    Task<FlowSimulatorSession> StepTickAsync(string flowId, string sessionId, CancellationToken cancellationToken);
    Task<FlowSimulatorSession> ApplyInputsAndStepAsync(string flowId, string sessionId, IReadOnlyList<EmulatorInputChange> inputs, CancellationToken cancellationToken);
    Task<FlowSimulatorSession> ApplyInputsAsync(string flowId, string sessionId, IReadOnlyList<EmulatorInputChange> inputs, CancellationToken cancellationToken);
    Task<FlowSimulatorSession> AdvanceAsync(string flowId, string sessionId, ulong milliseconds, CancellationToken cancellationToken);
    Task<FlowSimulatorSession> InjectFaultAsync(string flowId, string sessionId, string? fault, CancellationToken cancellationToken);
    Task<FlowSimulatorSession> ResetIoAsync(string flowId, string sessionId, bool powerCycle, CancellationToken cancellationToken);
    Task<FlowSimulatorSession> ResetInputsAsync(string flowId, string sessionId, CancellationToken cancellationToken);
    Task<FlowSimulatorSession> StepNodeAsync(string flowId, string sessionId, CancellationToken cancellationToken);
    Task<FlowSimulatorSession> StepInstructionAsync(string flowId, string sessionId, CancellationToken cancellationToken);
    Task<FlowSimulatorSession> RestartAsync(string flowId, string sessionId, CancellationToken cancellationToken);
    Task<FlowSimulatorSession> RunAsync(string flowId, string sessionId, uint intervalMilliseconds, CancellationToken cancellationToken);
    Task<FlowSimulatorSession> PauseAsync(string flowId, string sessionId, CancellationToken cancellationToken);
    Task KeepAliveAsync(string flowId, string sessionId, CancellationToken cancellationToken);
    Task StopAsync(string flowId, string sessionId, CancellationToken cancellationToken);
    Task ClearAsync(CancellationToken cancellationToken);
}