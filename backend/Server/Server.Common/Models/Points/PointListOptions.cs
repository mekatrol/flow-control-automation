namespace Server.Common.Models.Points;

public sealed record PointListOptions(
    string Filter,
    string? GroupId,
    int Page,
    int PageSize,
    string Sort);