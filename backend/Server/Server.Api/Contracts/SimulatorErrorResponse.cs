namespace Server.Api.Contracts;

public sealed record SimulatorErrorResponse(
    string Code,
    string Message,
    string? Path = null,
    string? NodeId = null,
    object? Details = null);