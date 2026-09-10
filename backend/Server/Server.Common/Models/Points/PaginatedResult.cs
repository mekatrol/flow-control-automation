namespace Server.Common.Models.Points;

public sealed record PaginatedResult<T>(
    IReadOnlyList<T> Items,
    int TotalItems,
    int Page,
    int PageSize,
    int PageCount);