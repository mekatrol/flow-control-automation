using Server.Common.Models.Communication;
using System.Text.Json.Nodes;

namespace Server.Api.Contracts;

public sealed record PointTestResult(
    string Operation,
    JsonNode? Value,
    string? Diagnostic,
    HttpResponsePreview HttpResponse);