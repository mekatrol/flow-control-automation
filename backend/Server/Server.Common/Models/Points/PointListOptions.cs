namespace Server.Common.Models.Points;

public sealed record PointListOptions(
    string Filter,
    int Page,
    int PageSize,
    string Sort);