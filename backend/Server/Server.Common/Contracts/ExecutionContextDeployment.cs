namespace Server.Common.Contracts;

public sealed record ExecutionContextDeployment
{
    public required string Id { get; init; }
    public required string ExecutionContextId { get; init; }
    public required int ExecutionContextRevision { get; init; }
    public required string ExecutionInstanceId { get; init; }
    public IReadOnlyList<PhysicalPointBinding> PhysicalPointBindings { get; init; } = [];
    public ExecutionContextDeploymentStatus Status { get; init; } = ExecutionContextDeploymentStatus.Draft;
    public int Generation { get; init; } = 1;
    public int Revision { get; init; } = 1;
}
