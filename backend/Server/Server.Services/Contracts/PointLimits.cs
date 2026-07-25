namespace Server.Services.Contracts;

public sealed record PointLimits(
    double? Minimum,
    double? Maximum,
    int? MaximumLength);