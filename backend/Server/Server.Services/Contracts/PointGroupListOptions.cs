namespace Server.Services.Contracts;

public sealed record PointGroupListOptions(
    string Filter,
    int Page,
    int PageSize,
    string Sort);