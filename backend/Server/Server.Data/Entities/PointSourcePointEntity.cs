namespace Server.Data.Entities;

/// <summary>Database-enforced ownership index for aggregate-owned points.</summary>
public sealed class PointSourcePointEntity
{
    public required string PointId { get; init; }
    public required string SourceId { get; init; }
}