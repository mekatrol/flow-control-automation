namespace Server.Common.Contracts;

public sealed record Flow
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = string.Empty;
    public string Status { get; init; } = "draft";
    public bool Disabled { get; init; }
    public required string UpdatedAt { get; init; }
    public IReadOnlyList<FlowNode> Nodes { get; init; } = [];
    public IReadOnlyList<FlowConnection> Connections { get; init; } = [];
    public FlowInterface Interface { get; init; } = new();
    public int Revision { get; init; } = 1;
    public IReadOnlyList<VirtualPointDeclaration> VirtualPointDeclarations { get; init; } = [];
}