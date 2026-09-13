namespace Server.Common.Models.Points;

public sealed record PointMappingCommandResult(
    string RenderedRequest,
    DateTimeOffset Timestamp,
    string? Diagnostic = null,
    HttpResponsePreview? Response = null);