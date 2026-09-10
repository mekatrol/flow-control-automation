namespace Server.Common.Models.Communication;

public sealed record HttpProtocolCheckResult(
    string? Diagnostic,
    HttpResponsePreview? Response = null);