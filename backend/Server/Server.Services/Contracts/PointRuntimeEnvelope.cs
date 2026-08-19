using System.Text.Json.Nodes;
using Server.Common.Contracts;

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