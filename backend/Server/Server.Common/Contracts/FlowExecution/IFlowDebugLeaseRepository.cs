namespace Server.Common.Contracts.FlowExecution;

public interface IFlowDebugLeaseRepository
{
    Task<FlowDebugLease> AcquireAsync(
        string flowId,
        string executionContextId,
        bool suspendedEnabledDeployment,
        CancellationToken cancellationToken);

    Task<FlowDebugLease?> GetByFlowAsync(string flowId, CancellationToken cancellationToken);

    Task HeartbeatAsync(
        string flowId,
        string executionContextId,
        CancellationToken cancellationToken);

    Task<bool> ReleaseAsync(
        string flowId,
        string executionContextId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<FlowDebugLease>> ReleaseExpiredAsync(
        DateTimeOffset heartbeatCutoff,
        CancellationToken cancellationToken);
}