using Server.Common.Types;
using System.Text.Json;

namespace Server.Common.Models;

public sealed record ExecutableFlowNode
{
    public required string Id { get; init; }

    public required FlowNodeType NodeType { get; init; }

    public IReadOnlyDictionary<string, JsonElement> Configuration { get; init; }
        = new Dictionary<string, JsonElement>();

    public string Label { get; init; } = string.Empty;

    public double X { get; init; }

    public double Y { get; init; }

    public double ZOrder { get; init; }

    public string? GroupId { get; init; }
}