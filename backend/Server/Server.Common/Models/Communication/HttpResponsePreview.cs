namespace Server.Common.Models.Communication;

public sealed record HttpResponsePreview(
    int StatusCode,
    string? ReasonPhrase,
    string? ContentType,
    string Body);