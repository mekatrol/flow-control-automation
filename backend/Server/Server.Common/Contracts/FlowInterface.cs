namespace Server.Common.Contracts;

public sealed record FlowInterface
{
    public int SchemaVersion { get; init; } = 1;
    public IReadOnlyList<FlowInterfaceInput> Inputs { get; init; } = [];
    public IReadOnlyList<FlowInterfaceOutput> Outputs { get; init; } = [];
}