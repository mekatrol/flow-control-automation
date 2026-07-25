namespace Server.Services.Contracts;

public sealed record FlowListOptions(
    string Filter = "",
    IReadOnlyList<string>? Statuses = null,
    int Page = 1,
    int PageSize = 20,
    string SortDirection = "ascending");