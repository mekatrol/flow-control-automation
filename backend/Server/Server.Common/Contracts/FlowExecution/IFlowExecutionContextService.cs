namespace Server.Common.Contracts.FlowExecution;

/// <summary>Creates and controls mode-neutral flow execution contexts.</summary>
public interface IFlowExecutionContextService
{
    Task<FlowExecutionContext> CreateAsync(CreateFlowExecutionContext request, CancellationToken cancellationToken);
    Task<FlowExecutionContext> GetAsync(string contextId, CancellationToken cancellationToken);
    Task<FlowExecutionContext> RunAsync(string contextId, RunFlowExecution request, CancellationToken cancellationToken);
    Task<FlowExecutionContext> PauseAsync(string contextId, CancellationToken cancellationToken);
    Task<FlowExecutionContext> StopAsync(string contextId, CancellationToken cancellationToken);
    Task<FlowExecutionContext> RestartAsync(string contextId, CancellationToken cancellationToken);
    Task<FlowExecutionContext> StepTickAsync(string contextId, CancellationToken cancellationToken);
    Task<FlowExecutionContext> StepNodeAsync(string contextId, CancellationToken cancellationToken);
    Task<FlowExecutionContext> StepInstructionAsync(string contextId, CancellationToken cancellationToken);
    Task<FlowExecutionContext> RunToAsync(string contextId, FlowDebugBreakpoint boundary, CancellationToken cancellationToken);
    Task<FlowExecutionContext> ReplaceBreakpointsAsync(string contextId, IReadOnlyList<FlowDebugBreakpoint> breakpoints, CancellationToken cancellationToken);
    Task<FlowExecutionContext> ApplyInputsAsync(string contextId, ApplyFlowExecutionInputs request, CancellationToken cancellationToken);
    Task<FlowExecutionContext> AdvanceAsync(string contextId, AdvanceFlowExecution request, CancellationToken cancellationToken);
    Task<FlowExecutionContext> InjectFaultAsync(string contextId, InjectFlowExecutionFault request, CancellationToken cancellationToken);
    Task<FlowExecutionContext> ResetIoAsync(string contextId, ResetFlowExecutionIo request, CancellationToken cancellationToken);
    Task<FlowExecutionContext> ResetInputsAsync(string contextId, CancellationToken cancellationToken);
    Task<FlowExecutionContext> EnableLiveOutputAsync(string contextId, EnableFlowExecutionLiveOutput request, CancellationToken cancellationToken);
    Task<FlowExecutionContext> KeepAliveAsync(string contextId, CancellationToken cancellationToken);
}