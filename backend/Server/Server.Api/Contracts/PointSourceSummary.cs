namespace Server.Api.Contracts;

public sealed record PointSourceSummary(
    string Id,
    string Name,
    string? Description,
    bool Enabled,
    PointSourceKind Kind,
    int MappingCount,
    int PointCount,
    int Revision,
    string? UpdatedAt);