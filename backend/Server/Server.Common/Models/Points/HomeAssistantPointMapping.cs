namespace Server.Common.Models.Points;

public sealed record HomeAssistantPointMapping(
    string EntityId,
    string? StateProperty,
    string? CommandService) : PointMapping;