namespace Server.Data.Entities;

public sealed class FlowEntity : BaseEntity
{
    public DateTimeOffset? LastExecutedAt { get; set; }
}