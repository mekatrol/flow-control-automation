namespace Server.Common.Models.Points;

public sealed record PointMappingResolution(
    PointSource Source,
    PointMapping Mapping,
    string Alias,
    AutomationPoint Point);