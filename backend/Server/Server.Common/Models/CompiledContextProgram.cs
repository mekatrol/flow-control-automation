namespace Server.Common.Models;

public sealed record CompiledContextProgram
{
    public required string FlowId { get; init; }
    public required int FlowRevision { get; init; }
    public required string ExecutionContextId { get; init; }
    public required int ExecutionContextRevision { get; init; }
    public required string ExecutionInstanceId { get; init; }
    public required int ExecutionInstanceRevision { get; init; }
    public required string ControllerTemplateId { get; init; }
    public required int ControllerTemplateRevision { get; init; }
    public required string ArtifactBase64 { get; init; }
    public required string ArtifactSha256 { get; init; }
    public int ArtifactVersion { get; init; } = FlowILV1Format.Version;
}