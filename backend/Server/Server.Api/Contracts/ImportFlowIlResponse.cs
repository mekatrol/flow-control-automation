namespace Server.Api.Contracts;

public sealed record ImportFlowIlResponse(
    Server.Services.Contracts.Flow Flow,
    string RecoveryLevel,
    IReadOnlyList<string> Warnings,
    Server.Services.Contracts.FlowDecompilationProvenance Provenance,
    bool Saved);