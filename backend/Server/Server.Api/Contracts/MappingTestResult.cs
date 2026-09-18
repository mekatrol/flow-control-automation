using Server.Common.Models.Communication;

namespace Server.Api.Contracts;

public sealed record MappingTestResult(
    string SourceId,
    string MappingId,
    string Operation,
    IReadOnlyDictionary<string, string?>? Values,
    string? RenderedRequest,
    string? Diagnostic,
    HttpResponsePreview? HttpResponse);
