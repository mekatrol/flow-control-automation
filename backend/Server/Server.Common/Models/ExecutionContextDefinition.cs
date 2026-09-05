namespace Server.Common.Models;

public sealed record ExecutionContextDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public int Revision { get; init; } = 1;
    public IReadOnlyList<ExecutionContextProgram> Programs { get; init; } = [];
    public IReadOnlyList<VirtualPointDeclaration> PointContracts { get; init; } = [];
    public string SchedulingPolicy { get; init; } = "independent";
}