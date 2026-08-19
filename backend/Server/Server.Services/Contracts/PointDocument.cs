using Server.Common.Contracts;

namespace Server.Services.Contracts;

public sealed record PointDocument
{
    public int SchemaVersion { get; init; } = 1;
    public IReadOnlyList<PointGroup> Groups { get; init; } = [];
    public IReadOnlyList<FlowPoint> Points { get; init; } = [];
}