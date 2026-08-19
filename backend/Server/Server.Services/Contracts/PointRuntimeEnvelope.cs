using Server.Common.Contracts;
using System.Text.Json.Nodes;

namespace Server.Services.Contracts;

public sealed record PointRuntimeEnvelope(
    string PointId,
    JsonNode? Value,
    string? Units,
    DataQuality Quality,
    string Reliability,
    string? SourceTimestamp,
    string? UpdatedAt,
    string ConnectionState,
    string Status,
    string Diagnostic);