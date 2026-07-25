namespace Server.Api.Contracts;

public sealed record DefinitionErrorResponse(
    string Message,
    string Code,
    object? Details = null);