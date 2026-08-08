using System.Text.Json;

namespace Server.Services.Contracts;

public sealed record ExecutableFlowNode
{
    public required string Id { get; init; }
    public required string Kind { get; init; }
    public IReadOnlyDictionary<string, JsonElement> Configuration { get; init; }
        = new Dictionary<string, JsonElement>();
}
