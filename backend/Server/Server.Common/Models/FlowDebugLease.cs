namespace Server.Common.Models;

public sealed record FlowDebugLease(
    string FlowId,
    string ExecutionContextId,
    DateTimeOffset StartedAt,
    DateTimeOffset LastHeartbeatAt,
    bool SuspendedEnabledDeployment,
    int RowVersion);