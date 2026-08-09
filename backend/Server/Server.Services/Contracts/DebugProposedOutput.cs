namespace Server.Services.Contracts;

public sealed record DebugProposedOutput(
    string PointId,
    string State,
    string Quality,
    bool ProposedValue);
