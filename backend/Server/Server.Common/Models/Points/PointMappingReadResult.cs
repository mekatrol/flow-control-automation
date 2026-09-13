namespace Server.Common.Models.Points;

public sealed record PointMappingReadResult(
    IReadOnlyDictionary<string, string?> Values,
    DataQualityType Quality,
    DateTimeOffset Timestamp,
    string? Diagnostic = null,
    HttpResponsePreview? Response = null);