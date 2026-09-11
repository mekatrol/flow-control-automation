namespace Server.Common.Models.Points;

public sealed record PointDocument
{
    public int SchemaVersion { get; init; } = 1;
    public IReadOnlyList<AutomationPoint> Points { get; init; } = [];
}