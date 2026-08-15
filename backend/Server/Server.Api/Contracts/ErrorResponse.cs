namespace Server.Api.Contracts;

/// <summary>Provides a human-readable error for endpoints that do not expose a structured diagnostic contract.</summary>
/// <param name="Message">A non-empty display message describing the failure; it is diagnostic prose rather than a stable programmatic code.</param>
public sealed record ErrorResponse(string Message);