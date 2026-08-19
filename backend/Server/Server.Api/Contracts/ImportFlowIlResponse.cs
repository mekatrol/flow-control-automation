using Server.Common.Contracts;

namespace Server.Api.Contracts;

/// <summary>Returns a validated flow recovered from a portable Flow IL artifact.</summary>
/// <param name="Flow">The recovered current-schema flow; it is returned even for preview-only imports.</param>
/// <param name="RecoveryLevel">The stable recovery classification, such as <c>lossless</c> or <c>functional</c>, describing how much authoring metadata was retained.</param>
/// <param name="Warnings">Ordered non-fatal recovery diagnostics; the collection is empty when no information was lost.</param>
/// <param name="Provenance">Artifact identity and revision metadata used to explain where the recovered flow originated.</param>
/// <param name="Saved">Whether the recovered flow was durably persisted; <see langword="false"/> identifies a preview result.</param>
public sealed record ImportFlowIlResponse(
    Flow Flow,
    string RecoveryLevel,
    IReadOnlyList<string> Warnings,
    FlowDecompilationProvenance Provenance,
    bool Saved);