using System.Text.Json.Nodes;

namespace Server.Services.Contracts;

public sealed record Point
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public bool Enabled { get; init; }
    public string? GroupId { get; init; }
    public required string Implementation { get; init; }
    public required string Direction { get; init; }
    public required string ValueType { get; init; }
    public string? Units { get; init; }
    public JsonNode? StateLabels { get; init; }
    public bool Readable { get; init; }
    public bool Commandable { get; init; }
    public required string Persistence { get; init; }
    public JsonNode? RelinquishDefault { get; init; }
    public string? SourceId { get; init; }
    public JsonObject? Mapping { get; init; }
    public JsonObject? Limits { get; init; }
    public JsonObject? SafeDisablePolicy { get; init; }
}