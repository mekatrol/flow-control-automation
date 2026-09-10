namespace Server.Common.Models.Points;

public sealed record PointLimits(
    double? Minimum,
    double? Maximum,
    int? MaximumLength);