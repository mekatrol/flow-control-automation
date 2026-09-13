using System.Text.Json.Nodes;

namespace Server.Common.Models.Points;

public sealed record PointValueConversionResult(
    JsonNode? Value,
    DataQualityType Quality,
    string? Diagnostic = null);