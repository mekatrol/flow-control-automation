using System.Text.Json.Nodes;

namespace Server.Services.Contracts;

public sealed record PointGroup
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? SourceId { get; init; }
    public JsonObject MappingDefaults { get; init; } = [];
}