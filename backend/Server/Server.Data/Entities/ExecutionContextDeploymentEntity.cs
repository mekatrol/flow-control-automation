namespace Server.Data.Entities;

public sealed class ExecutionContextDeploymentEntity : BaseEntity
{
    public string ExecutionContextId { get; set; } = string.Empty;
    public string ExecutionInstanceId { get; set; } = string.Empty;
}