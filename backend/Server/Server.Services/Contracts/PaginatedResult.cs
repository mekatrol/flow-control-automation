namespace Server.Services.Contracts;

public sealed record PaginatedResult<T>(
    IReadOnlyList<T> Items,
    int TotalItems,
    int Page,
    int PageSize,
    int PageCount);