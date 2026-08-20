using Server.Common.Contracts;

namespace Server.Compiler.Contracts;

/// <summary>
/// Canonical executable artifact and the stable identifiers used to correlate snapshots.
/// </summary>
public sealed record FlowCompilationResult
{
    public int ArtifactVersion { get; init; } = FlowILV1Format.Version;

    public required ReadOnlyMemory<byte> Artifact { get; init; }

    public required string ArtifactSha256 { get; init; }

    public required uint FlowRevision { get; init; }

    public required string ControllerTemplateId { get; init; }

    public required int ControllerTemplateRevision { get; init; }

    public IReadOnlyDictionary<string, ushort> NodeIndices { get; init; } = new Dictionary<string, ushort>(StringComparer.Ordinal);

    public IReadOnlyList<string> Schedule { get; init; } = [];

    public uint MaximumWorkPerScan { get; init; }

    public uint WorkingBytes { get; init; }

    public uint MaximumSnapshotBytes { get; init; }

    public uint SectionCount { get; init; }

    public uint InstructionCount { get; init; }

    public uint SlotCount { get; init; }

    public uint PointCount { get; init; }

    public uint StateCount { get; init; }
}