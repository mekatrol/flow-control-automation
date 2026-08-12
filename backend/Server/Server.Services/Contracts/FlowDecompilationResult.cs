namespace Server.Services.Contracts;

public sealed record FlowDecompilationResult
{
    public required Flow Flow { get; init; }
    public string RecoveryLevel { get; init; } = "normalized";
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public required FlowDecompilationProvenance Provenance { get; init; }
}
