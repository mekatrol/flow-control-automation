namespace Server.Common.Contracts;

public sealed record ExecutableFlowSource
{
    public int SchemaVersion { get; init; } = 1;

    public required string Id { get; init; }

    public required uint Revision { get; init; }

    public required string ControllerTemplateId { get; init; }

    public required uint ControllerTemplateRevision { get; init; }

    public ExecutableFlowExecution Execution { get; init; } = new();

    public IReadOnlyList<ExecutableFlowNode> Nodes { get; init; } = [];

    public IReadOnlyList<ExecutableFlowConnection> Connections { get; init; } = [];

    public FlowInterface Interface { get; init; } = new();
}