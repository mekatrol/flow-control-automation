namespace Server.Services.Contracts;

public sealed record PointSourceListOptions(
    string Filter = "",
    int Page = 1,
    int PageSize = 10,
    string SortDirection = "ascending");