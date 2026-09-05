using Server.Common.Models;

namespace Server.Compiler.Contracts;

public sealed record FlowDecompilationResult
{
    public required Flow Flow { get; init; }
    public string RecoveryLevel { get; init; } = "lossless";
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public required FlowDecompilationProvenance Provenance { get; init; }
}