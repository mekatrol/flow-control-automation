namespace Server.Services.Contracts;

public sealed record HomeAssistantPointMapping(
    string EntityId,
    string? StateProperty,
    string? CommandService) : PointMapping;