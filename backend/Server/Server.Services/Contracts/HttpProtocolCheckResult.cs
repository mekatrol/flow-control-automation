namespace Server.Services.Contracts;

public sealed record HttpProtocolCheckResult(
    string? Diagnostic,
    HttpResponsePreview? Response = null);
