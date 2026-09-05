namespace Server.Common.Models;

public sealed record FlowVersionSnapshot
{
    public required string Name { get; init; }
    public string Description { get; init; } = string.Empty;
    public required string UpdatedAt { get; init; }
    public required int Revision { get; init; }
    public IReadOnlyList<FlowNode> Nodes { get; init; } = [];
    public IReadOnlyList<FlowConnection> Connections { get; init; } = [];
}