using Server.Common.Contracts;
using Server.Common.Types;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Server.Common.Models;

public sealed record FlowPoint : IAutomationPoint
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public bool Enabled { get; set; }

    public string? GroupId { get; set; }

    public required string Implementation { get; init; }

    public required DataDirectionType Direction { get; init; }

    public required FlowPointValueType ValueType { get; init; }

    public required PointSourceType PointSourceType { get; init; }

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

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Revision { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CreatedAt { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UpdatedAt { get; init; }
}