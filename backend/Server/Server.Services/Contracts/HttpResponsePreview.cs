namespace Server.Services.Contracts;

public sealed record HttpResponsePreview(
    int StatusCode,
    string? ReasonPhrase,
    string? ContentType,
    string Body);
