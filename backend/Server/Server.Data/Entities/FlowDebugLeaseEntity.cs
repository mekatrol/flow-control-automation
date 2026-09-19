namespace Server.Data.Entities;

public sealed class FlowDebugLeaseEntity
{
    public string FlowId { get; set; } = string.Empty;

    public string ExecutionContextId { get; set; } = string.Empty;

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset LastHeartbeatAt { get; set; }

    public bool SuspendedEnabledDeployment { get; set; }

    public int RowVersion { get; set; }
}