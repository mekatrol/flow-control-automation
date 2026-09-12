using System.Text.Json.Nodes;

namespace Server.Common.Models.Points;

public sealed record MappingCommandOperation
{
    public string? Path { get; init; }
    public string? Method { get; init; }
    public string? Format { get; init; }
    public string? ContentType { get; init; }
    public string? Template { get; init; }
    public string? Topic { get; init; }
    public int? Qos { get; init; }
    public bool? Retain { get; init; }
    public string? Service { get; init; }
    public JsonObject? ServiceData { get; init; }
}