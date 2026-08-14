using System.Text.Json;

namespace Server.Services.Contracts;

public sealed record FlowInterfaceInput
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string DataType { get; init; }
    public string? Units { get; init; }
    public JsonElement? DefaultValue { get; init; }
    public bool Required { get; init; }
}
