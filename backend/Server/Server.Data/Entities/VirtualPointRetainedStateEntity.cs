namespace Server.Data.Entities;

public sealed class VirtualPointRetainedStateEntity : BaseEntity
{
    public string ExecutionInstanceId { get; set; } = string.Empty;

    public string PointKey { get; set; } = string.Empty;
}