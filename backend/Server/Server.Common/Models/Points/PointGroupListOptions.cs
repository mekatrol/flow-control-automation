namespace Server.Common.Models.Points;

public sealed record PointGroupListOptions(
    string Filter,
    int Page,
    int PageSize,
    string Sort);