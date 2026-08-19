using Server.Api.Contracts;
using Server.Common.Contracts;
using Server.Services;
using Server.Services.Contracts;
using System.Globalization;
using System.Text;

namespace Server.Api.Extensions;

public static class PointDefinitionEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapPointDefinitionEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/points", ListPoints);
        endpoints.MapPost("/api/points", CreatePoint);
        endpoints.MapGet("/api/points/{pointId}", GetPoint);
        endpoints.MapPut("/api/points/{pointId}", UpdatePoint);
        endpoints.MapDelete("/api/points/{pointId}", DeletePoint);
        endpoints.MapGet("/api/points/{pointId}/runtime", GetPointRuntime);
        endpoints.MapGet("/api/point-groups", ListGroups);
        endpoints.MapPost("/api/point-groups", CreateGroup);
        endpoints.MapGet("/api/point-groups/{groupId}", GetGroup);
        endpoints.MapPut("/api/point-groups/{groupId}", UpdateGroup);
        endpoints.MapDelete("/api/point-groups/{groupId}", DeleteGroup);
        endpoints.MapPost(
            "/api/point-groups/{groupId}/make-points-standalone",
            MakePointsStandalone);
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
        IEnumerable<FlowPoint> filtered = all;
        if (!string.IsNullOrWhiteSpace(options.Value!.Filter))
        {
            filtered = filtered.Where(point =>
                Contains(point.Name, options.Value.Filter)
                || Contains(point.Id, options.Value.Filter)
                || Contains(point.Description, options.Value.Filter));
        }

        if (options.Value.GroupId is not null)
        {
            filtered = options.Value.GroupId.Length == 0
                ? filtered.Where(point => point.GroupId is null)
                : filtered.Where(point => point.GroupId == options.Value.GroupId);
        }

        return Results.Json(Page(
            Sort(filtered, options.Value.Sort, point => point.Name, point => point.Id),
            options.Value.Page,
            options.Value.PageSize));
    }

    private static async Task<IResult> ListGroups(
        HttpRequest request,
        IPointDefinitionStore definitions,
        CancellationToken cancellationToken)
    {
        var options = ParseGroupListOptions(request);
        if (options.Error is not null)
        {
            return options.Error;
        }

        var all = await definitions.ListGroupsAsync(cancellationToken);
        var filtered = string.IsNullOrWhiteSpace(options.Value!.Filter)
            ? all
            : all.Where(group =>
                Contains(group.Name, options.Value.Filter)
                || Contains(group.Id, options.Value.Filter)
                || Contains(group.Description, options.Value.Filter));
        return Results.Json(Page(
            Sort(filtered, options.Value.Sort, group => group.Name, group => group.Id),
            options.Value.Page,
            options.Value.PageSize));
    }

    private static async Task<IResult> CreatePoint(
        HttpRequest request,
        HttpResponse response,
        IPointDefinitionStore definitions,
        CancellationToken cancellationToken)
    {
        var decoded = await Decode(request, PointYaml.Parse, cancellationToken);
        return decoded.Error
            ?? await Write(
                response,
                () => definitions.CreatePointAsync(decoded.Value!, cancellationToken),
                PointYaml.Render,
                StatusCodes.Status201Created);
    }

    private static async Task<IResult> GetPoint(
        string pointId,
        HttpResponse response,
        IPointDefinitionStore definitions,
        CancellationToken cancellationToken) =>
        await Write(
            response,
            () => definitions.GetPointAsync(pointId, cancellationToken),
            PointYaml.Render,
            StatusCodes.Status200OK);

    private static async Task<IResult> UpdatePoint(
        string pointId,
        HttpRequest request,
        HttpResponse response,
        IPointDefinitionStore definitions,
        CancellationToken cancellationToken)
    {
        var decoded = await Decode(request, PointYaml.Parse, cancellationToken);
        return decoded.Error ?? (TryRevision(request.Headers.IfMatch.ToString(), "If-Match", out var revision)
            ? await Write(
                response,
                () => definitions.UpdatePointAsync(
                    pointId,
                    decoded.Value!,
                    revision,
                    cancellationToken),
                PointYaml.Render,
                StatusCodes.Status200OK)
            : Error(400, "invalid_revision", "If-Match must contain the last observed revision"));
    }

    private static async Task<IResult> DeletePoint(
        string pointId,
        HttpRequest request,
        IPointDefinitionStore definitions,
        CancellationToken cancellationToken)
    {
        if (!TryRevision(request.Query["revision"].ToString(), "revision", out var revision))
        {
            return Error(400, "invalid_revision", "revision must be a positive integer");
        }

        return await Delete(
            () => definitions.DeletePointAsync(pointId, revision, cancellationToken));
    }

    private static async Task<IResult> CreateGroup(
        HttpRequest request,
        HttpResponse response,
        IPointDefinitionStore definitions,
        CancellationToken cancellationToken)
    {
        var decoded = await Decode(request, PointGroupYaml.Parse, cancellationToken);
        return decoded.Error
            ?? await Write(
                response,
                () => definitions.CreateGroupAsync(decoded.Value!, cancellationToken),
                PointGroupYaml.Render,
                StatusCodes.Status201Created);
    }

    private static async Task<IResult> GetGroup(
        string groupId,
        HttpResponse response,
        IPointDefinitionStore definitions,
        CancellationToken cancellationToken) =>
        await Write(
            response,
            () => definitions.GetGroupAsync(groupId, cancellationToken),
            PointGroupYaml.Render,
            StatusCodes.Status200OK);

    private static async Task<IResult> UpdateGroup(
        string groupId,
        HttpRequest request,
        HttpResponse response,
        IPointDefinitionStore definitions,
        CancellationToken cancellationToken)
    {
        var decoded = await Decode(request, PointGroupYaml.Parse, cancellationToken);
        return decoded.Error ?? (TryRevision(request.Headers.IfMatch.ToString(), "If-Match", out var revision)
            ? await Write(
                response,
                () => definitions.UpdateGroupAsync(
                    groupId,
                    decoded.Value!,
                    revision,
                    cancellationToken),
                PointGroupYaml.Render,
                StatusCodes.Status200OK)
            : Error(400, "invalid_revision", "If-Match must contain the last observed revision"));
    }

    private static async Task<IResult> DeleteGroup(
        string groupId,
        HttpRequest request,
        IPointDefinitionStore definitions,
        CancellationToken cancellationToken)
    {
        if (!TryRevision(request.Query["revision"].ToString(), "revision", out var revision))
        {
            return Error(400, "invalid_revision", "revision must be a positive integer");
        }

        try
        {
            await definitions.DeleteGroupAsync(groupId, revision, cancellationToken);
            return Results.NoContent();
        }
        catch (PointDefinitionConflictException exception)
        {
            var pointIds = (await definitions.ListPointsAsync(cancellationToken))
                .Where(point => point.GroupId == groupId)
                .Select(point => point.Id)
                .Order(StringComparer.Ordinal)
                .ToArray();
            return Error(
                409,
                ConflictCode(exception),
                exception.Message,
                pointIds.Length == 0 ? null : new { pointIds });
        }
        catch (PointDefinitionNotFoundException)
        {
            return Error(404, "not_found", "point group not found");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Error(500, "persistence_failed", "unable to persist point definition");
        }
    }

    private static async Task<IResult> MakePointsStandalone(
        string groupId,
        HttpRequest request,
        IPointDefinitionStore definitions,
        CancellationToken cancellationToken)
    {
        if (!TryRevision(request.Query["revision"].ToString(), "revision", out var revision))
        {
            return Error(400, "invalid_revision", "revision must be a positive integer");
        }

        try
        {
            var points = await definitions.MakePointsStandaloneAsync(
                groupId,
                revision,
                cancellationToken);
            return Results.Json(new { items = points, updatedItems = points.Count });
        }
        catch (PointDefinitionNotFoundException)
        {
            return Error(404, "not_found", "point group not found");
        }
        catch (PointDefinitionConflictException exception)
        {
            return Error(409, ConflictCode(exception), exception.Message);
        }
        catch (PointDefinitionValidationException exception)
        {
            return Error(409, "membership_conflict", exception.Message);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Error(500, "persistence_failed", "unable to persist point definition");
        }
    }

    private static async Task<IResult> Delete(Func<Task> operation)
    {
        try
        {
            await operation();
            return Results.NoContent();
        }
        catch (PointDefinitionNotFoundException)
        {
            return Error(404, "not_found", "point definition not found");
        }
        catch (PointDefinitionConflictException exception)
        {
            return Error(409, ConflictCode(exception), exception.Message);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Error(500, "persistence_failed", "unable to persist point definition");
        }
    }

    private static async Task<IResult> Write<T>(
        HttpResponse response,
        Func<Task<T>> operation,
        Func<T, string> render,
        int status)
        where T : class
    {
        try
        {
            var value = await operation();
            var revision = value switch
            {
                FlowPoint point => point.Revision,
                PointGroup group => group.Revision,
                _ => throw new InvalidOperationException("Unsupported point resource."),
            };
            response.Headers.ETag = revision.ToString(CultureInfo.InvariantCulture);
            return Results.Text(render(value), "application/yaml", Encoding.UTF8, status);
        }
        catch (PointDefinitionNotFoundException exception)
        {
            return Error(404, "not_found", exception.Message);
        }
        catch (PointDefinitionConflictException exception)
        {
            return Error(409, ConflictCode(exception), exception.Message);
        }
        catch (PointDefinitionValidationException exception)
        {
            return Error(400, "validation_failed", exception.Message);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Error(500, "persistence_failed", "unable to persist point definition");
        }
    }

    private static async Task<(T? Value, IResult? Error)> Decode<T>(
        HttpRequest request,
        Func<ReadOnlySpan<byte>, T> parse,
        CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            var buffer = new byte[ConfigurationYaml.MaximumBytes + 1];
            var length = 0;
            while (length < buffer.Length)
            {
                var read = await request.Body.ReadAsync(
                    buffer.AsMemory(length, buffer.Length - length),
                    cancellationToken);
                if (read == 0)
                {
                    break;
                }

                length += read;
            }

            if (length > ConfigurationYaml.MaximumBytes)
            {
                return (null, Error(400, "request_too_large", "unable to read YAML request"));
            }

            return (parse(buffer.AsSpan(0, length)), null);
        }
        catch (ConfigurationYamlException exception)
        {
            return (null, Error(400, YamlCode(exception.Category), exception.Message));
        }
    }

    private static (PointListOptions? Value, IResult? Error) ParsePointListOptions(
        HttpRequest request)
    {
        var common = ParseCommonListOptions(request);
        if (common.Error is not null)
        {
            return (null, common.Error);
        }

        var groupValues = request.Query["groupId"];
        if (groupValues.Count > 1)
        {
            return (null, Error(400, "invalid_query", "groupId must be specified once"));
        }

        return (new PointListOptions(
            common.Filter!,
            groupValues.Count == 0 ? null : groupValues.ToString(),
            common.Page,
            common.PageSize,
            common.Sort!), null);
    }

    private static (PointGroupListOptions? Value, IResult? Error) ParseGroupListOptions(
        HttpRequest request)
    {
        var common = ParseCommonListOptions(request);
        return common.Error is null
            ? (new PointGroupListOptions(
                common.Filter!,
                common.Page,
                common.PageSize,
                common.Sort!), null)
            : (null, common.Error);
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

    private static bool TryRevision(string value, string _, out int revision) =>
        int.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out revision)
        && revision > 0;

    private static string ConflictCode(PointDefinitionConflictException exception) =>
        exception.Message.Contains("stale revision", StringComparison.Ordinal)
            ? "stale_revision"
            : "resource_conflict";

    private static string YamlCode(ConfigurationYamlError error) => error switch
    {
        ConfigurationYamlError.Syntax => "yaml_syntax",
        ConfigurationYamlError.TooLarge => "request_too_large",
        ConfigurationYamlError.MultipleDocuments => "multiple_yaml_documents",
        ConfigurationYamlError.UnsupportedFeature => "unsupported_yaml",
        ConfigurationYamlError.UnsupportedSchema => "unsupported_schema",
        _ => "invalid_yaml",
    };

    private static IResult Error(
        int status,
        string code,
        string message,
        object? details = null) =>
        Results.Json(
            new DefinitionErrorResponse(message, code, details),
            statusCode: status);
}