namespace Server.Common.Models.Communication;

public sealed record ConnectivityStage(
    string Name,
    string Status,
    string? Diagnostic = null);