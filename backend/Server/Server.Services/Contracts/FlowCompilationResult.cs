namespace Server.Services.Contracts;

/// <summary>
/// Canonical executable artifact and the stable identifiers used to correlate snapshots.
/// </summary>
public sealed record FlowCompilationResult
{
    public required ReadOnlyMemory<byte> Artifact { get; init; }
    public required string ArtifactSha256 { get; init; }
    public required uint FlowRevision { get; init; }
    public required string ControllerTemplateId { get; init; }
    public required int ControllerTemplateRevision { get; init; }
    public IReadOnlyDictionary<string, ushort> NodeIndices { get; init; }
        = new Dictionary<string, ushort>(StringComparer.Ordinal);
}
