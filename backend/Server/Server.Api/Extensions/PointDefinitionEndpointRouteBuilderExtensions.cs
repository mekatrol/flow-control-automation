using Server.Api.Contracts;
using Server.Services;
using System.Globalization;

namespace Server.Api.Extensions;

public static class PointDefinitionEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapPointDefinitionEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/points", ListPoints);
        endpoints.MapGet("/api/points/{pointId}/runtime", GetPointRuntime);

        return endpoints;
    }

    private static async Task<IResult> GetPointRuntime(
        string pointId,
        IPointReadService reader,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Json(await reader.ReadAsync(pointId, cancellationToken));
        }
        catch (PointDefinitionNotFoundException)
        {
            return Error(404, "not_found", "point definition not found");
        }
    }

    private static async Task<IResult> ListPoints(
        HttpRequest request,
        IPointDefinitionStore definitions,
        CancellationToken cancellationToken)
    {
        var options = ParsePointListOptions(request);

        if (options.Error is not null)
        {
            return options.Error;
        }

        var all = await definitions.ListPointsAsync(cancellationToken);
        IEnumerable<AutomationPoint> filtered = all;

        if (!string.IsNullOrWhiteSpace(options.Value!.Filter))
        {
            filtered = filtered.Where(point =>
                Contains(point.Name, options.Value.Filter)
                || Contains(point.Id, options.Value.Filter)
                || Contains(point.Description, options.Value.Filter));
        }

        return Results.Json(Page(
            Sort(filtered, options.Value.Sort, point => point.Name, point => point.Id),
            options.Value.Page,
            options.Value.PageSize));
    }

    private static (PointListOptions? Value, IResult? Error) ParsePointListOptions(
        HttpRequest request)
    {
        var common = ParseCommonListOptions(request);

        if (common.Error is not null)
        {
            return (null, common.Error);
        }

        return (new PointListOptions(
            common.Filter!,
            common.Page,
            common.PageSize,
            common.Sort!), null);
    }

    private static (
        string? Filter,
        int Page,
        int PageSize,
        string? Sort,
        IResult? Error) ParseCommonListOptions(HttpRequest request)
    {
        if (!PositiveInteger(request.Query["page"].ToString(), 1, out var page)
            || !PositiveInteger(request.Query["pageSize"].ToString(), 10, out var pageSize)
            || pageSize is not (10 or 20 or 50))
        {
            return (null, 0, 0, null, Error(
                400,
                "invalid_query",
                "invalid pagination or sort query"));
        }

        var sort = request.Query["sort"].ToString();
        sort = sort.Length == 0 ? "ascending" : sort;

        if (sort is not ("ascending" or "descending"))
        {
            return (null, 0, 0, null, Error(
                400,
                "invalid_query",
                "invalid pagination or sort query"));
        }

        return (request.Query["filter"].ToString(), page, pageSize, sort, null);
    }

    private static PaginatedResult<T> Page<T>(
        IEnumerable<T> items,
        int page,
        int pageSize)
    {
        var materialized = items.ToArray();

        return new PaginatedResult<T>(
            [.. materialized.Skip((page - 1) * pageSize).Take(pageSize)],
            materialized.Length,
            page,
            pageSize,
            (int)Math.Ceiling(materialized.Length / (double)pageSize));
    }

    private static IEnumerable<T> Sort<T>(
        IEnumerable<T> values,
        string direction,
        Func<T, string> name,
        Func<T, string> id) =>
        direction == "ascending"
            ? values.OrderBy(name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(id, StringComparer.Ordinal)
            : values.OrderByDescending(name, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(id, StringComparer.Ordinal);

    private static bool Contains(string? value, string filter) =>
        value?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true;

    private static bool PositiveInteger(string value, int fallback, out int result)
    {
        if (value.Length == 0)
        {
            result = fallback;

            return true;
        }

        return int.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out result)
            && result > 0;
    }

    private static IResult Error(
        int status,
        string code,
        string message,
        object? details = null) =>
        Results.Json(
            new DefinitionErrorResponse(message, code, details),
            statusCode: status);
}