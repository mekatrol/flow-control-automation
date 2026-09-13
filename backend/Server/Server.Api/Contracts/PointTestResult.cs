using Server.Common.Models.Communication;
using System.Text.Json.Nodes;

namespace Server.Api.Contracts;

public sealed record PointTestResult(
    string SourceId,
    string PointId,
    string MappingId,
    string Alias,
    string Operation,
    JsonNode? Value,
    DataQualityType? Quality,
    string? RenderedRequest,
    string? Diagnostic,
    HttpResponsePreview? HttpResponse);