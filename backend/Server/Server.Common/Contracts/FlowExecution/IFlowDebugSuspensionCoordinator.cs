namespace Server.Common.Contracts.FlowExecution;

public interface IFlowDebugSuspensionCoordinator
{
    Task<FlowDebugLease> AcquireAsync(Flow flow, string contextId, CancellationToken cancellationToken);

    Task<bool> ReleaseAsync(
        Flow flow,
        string contextId,
        CancellationToken cancellationToken);

    Task EnsureAvailableAsync(string flowId, CancellationToken cancellationToken);
}