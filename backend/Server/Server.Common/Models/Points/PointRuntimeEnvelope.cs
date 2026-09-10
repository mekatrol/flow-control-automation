using System.Text.Json.Nodes;

namespace Server.Common.Models.Points;

public sealed record PointRuntimeEnvelope(
    string PointId,
    JsonNode? Value,
    string? Units,
    DataQualityType Quality,
    string Reliability,
    string? SourceTimestamp,
    string? UpdatedAt,
    string ConnectionState,
    string Status,
    string Diagnostic,
    HttpResponsePreview? DeviceResponse = null);