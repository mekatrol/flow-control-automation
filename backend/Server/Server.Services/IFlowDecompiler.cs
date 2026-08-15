using Server.Services.Contracts;

namespace Server.Services;

/// <summary>Validates portable Flow IL and recovers the current authoring-flow representation.</summary>
public interface IFlowDecompiler
{
    /// <summary>Authenticates and decodes one complete Flow IL artifact without persisting the recovered flow.</summary>
    /// <param name="artifact">The non-empty bounded binary artifact, including its envelope, section table, and digests; only the current supported IL version is accepted.</param>
    /// <param name="name">An optional non-empty display-name override; <see langword="null"/> uses recoverable artifact metadata or the canonical fallback.</param>
    /// <returns>The recovered flow, provenance, recovery classification, and ordered non-fatal warnings.</returns>
    /// <exception cref="FlowDecompilationException">Thrown when the artifact is malformed, corrupt, unsupported, or cannot represent a valid current flow.</exception>
    FlowDecompilationResult Decompile(ReadOnlyMemory<byte> artifact, string? name = null);
}