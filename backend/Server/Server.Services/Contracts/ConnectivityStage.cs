namespace Server.Services.Contracts;

public sealed record ConnectivityStage(
    string Name,
    string Status,
    string? Diagnostic = null);