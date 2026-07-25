namespace Server.Services.Contracts;

public sealed record PointSourceDocument
{
    public int SchemaVersion { get; init; } = 1;
    public IReadOnlyList<PointSource> Sources { get; init; } = [];
}