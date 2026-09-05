using Server.Common.Types;

namespace Server.Common.Models;

public sealed record ExecutionContextDeployment
{
    public required string Id { get; init; }
    public required string ExecutionContextId { get; init; }
    public required int ExecutionContextRevision { get; init; }
    public required string ExecutionInstanceId { get; init; }
    public IReadOnlyList<PhysicalPointBinding> PhysicalPointBindings { get; init; } = [];
    public IReadOnlyList<CompiledContextProgram> CompiledPrograms { get; init; } = [];
    public ExecutionContextDeploymentStatusType Status { get; init; } = ExecutionContextDeploymentStatusType.Draft;
    public int Generation { get; init; } = 1;
    public int Revision { get; init; } = 1;
}