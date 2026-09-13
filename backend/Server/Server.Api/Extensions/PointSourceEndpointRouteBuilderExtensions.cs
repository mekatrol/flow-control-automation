using Server.Api.Contracts;
using Server.Common.Models.Communication;
using Server.Services;
using System.Globalization;
using System.Text;

namespace Server.Api.Extensions;

public static class PointSourceEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapPointSourceEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/point-sources", List);
        endpoints.MapPost("/api/point-sources", Create);
        endpoints.MapGet("/api/point-sources/{sourceId}", Get);
        endpoints.MapPut("/api/point-sources/{sourceId}", Update);
        endpoints.MapDelete("/api/point-sources/{sourceId}", Delete);
        endpoints.MapPost("/api/point-sources/test", TestUnsaved);
        endpoints.MapPost("/api/point-sources/test-point", TestUnsavedPoint);
        endpoints.MapPost("/api/point-sources/{sourceId}/test", TestSaved);
        endpoints.MapPost("/api/point-sources/{sourceId}/points/{pointId}/test", TestSavedPoint);

        return endpoints;
    }

    private static async Task<IResult> List(
        HttpRequest request,
        IPointSourceService sources,
        CancellationToken cancellationToken)
    {
        var query = request.Query;

        if (!PositiveInteger(query["page"].ToString(), 1, out var page)
            || !PositiveInteger(query["pageSize"].ToString(), 10, out var pageSize)
            || pageSize is not (10 or 20 or 50))
        {
            return Error(StatusCodes.Status400BadRequest, "invalid pagination or sort query");
        }

        var sort = query["sort"].ToString();
        sort = sort.Length == 0 ? "ascending" : sort;

        if (sort is not ("ascending" or "descending"))
        {
            return Error(StatusCodes.Status400BadRequest, "invalid pagination or sort query");
        }

        try
        {
            var result = await sources.ListAsync(
                new PointSourceListOptions(
                    query["filter"].ToString(),
                    page,
                    pageSize,
                    sort),
                cancellationToken);

            return Results.Json(new PaginatedResult<PointSourceSummary>(
                [.. result.Items.Select(Summary)], result.TotalItems, result.Page,
                result.PageSize, result.PageCount));
        }
        catch (PointSourceValidationException exception)
        {
            return Error(StatusCodes.Status400BadRequest, exception.Message);
        }
        catch (PointDefinitionValidationException exception)
        {
            return Error(StatusCodes.Status400BadRequest, exception.Message);
        }
    }

    private static async Task<IResult> Create(
        HttpRequest request,
        HttpResponse response,
        IPointSourceService sources,
        CancellationToken cancellationToken)
    {
        var decoded = await Decode(request, cancellationToken);

        return decoded.Error ?? await WriteSource(
            response,
            () => sources.CreateAsync(decoded.Source!, cancellationToken),
            StatusCodes.Status201Created);
    }

    private static async Task<IResult> Get(
        string sourceId,
        HttpResponse response,
        IPointSourceService sources,
        CancellationToken cancellationToken) =>
        await WriteSource(
            response,
            () => sources.GetAsync(sourceId, cancellationToken),
            StatusCodes.Status200OK);

    private static async Task<IResult> Update(
        string sourceId,
        HttpRequest request,
        HttpResponse response,
        IPointSourceService sources,
        CancellationToken cancellationToken)
    {
        var decoded = await Decode(request, cancellationToken);

        if (decoded.Error is not null)
        {
            return decoded.Error;
        }

        if (!int.TryParse(
            request.Headers.IfMatch.ToString(),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var revision))
        {
            return Error(
                StatusCodes.Status400BadRequest,
                "If-Match must contain the last observed revision");
        }

        return await WriteSource(
            response,
            () => sources.UpdateAsync(
                sourceId,
                decoded.Source!,
                revision,
                cancellationToken),
            StatusCodes.Status200OK);
    }

    private static async Task<IResult> Delete(
        string sourceId,
        HttpRequest request,
        IPointSourceService sources,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(
            request.Query["revision"].ToString(),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var revision))
        {
            return Error(StatusCodes.Status400BadRequest, "revision must be an integer");
        }

        try
        {
            await sources.DeleteAsync(sourceId, revision, cancellationToken);

            return Results.NoContent();
        }
        catch (PointSourceNotFoundException)
        {
            return Error(StatusCodes.Status404NotFound, "point source not found");
        }
        catch (PointSourceConflictException exception)
        {
            return Error(StatusCodes.Status409Conflict, exception.Message);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Error(
                StatusCodes.Status500InternalServerError,
                "unable to persist point source");
        }
    }

    private static async Task<IResult> TestUnsaved(
        HttpRequest request,
        IConnectivityService connectivity,
        CancellationToken cancellationToken)
    {
        var decoded = await Decode(request, cancellationToken);

        return decoded.Error
            ?? Results.Json(await connectivity.TestAsync(
                decoded.Source!,
                ClientKey(request),
                cancellationToken));
    }

    private static async Task<IResult> TestSaved(
        string sourceId,
        HttpRequest request,
        IPointSourceService sources,
        IConnectivityService connectivity,
        CancellationToken cancellationToken)
    {
        try
        {
            var source = await sources.GetAsync(sourceId, cancellationToken);

            return Results.Json(await connectivity.TestAsync(
                source,
                ClientKey(request),
                cancellationToken));
        }
        catch (PointSourceNotFoundException)
        {
            return Error(StatusCodes.Status404NotFound, "point source not found");
        }
    }

    private static async Task<IResult> TestUnsavedPoint(
        PointTestRequest request,
        IPointSourceValidator sourceValidator,
        IPointMappingResolver resolver,
        IPointMappingExecutionService execution,
        IPointValueConverter values,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.SourceYaml))
            {
                return Error(StatusCodes.Status400BadRequest, "sourceYaml is required");
            }

            if (string.IsNullOrWhiteSpace(request.PointId))
            {
                return Error(StatusCodes.Status400BadRequest, "pointId is required");
            }

            var source = PointSourceYaml.Parse(Encoding.UTF8.GetBytes(request.SourceYaml));
            sourceValidator.Validate(source);

            return await ExecutePointTest(source, request.PointId, request.Operation,
                request.Value, resolver, execution, values, cancellationToken);
        }
        catch (Exception exception) when (exception is ConfigurationYamlException
            or PointSourceValidationException
            or ArgumentException
            or InvalidOperationException)
        {
            return Error(StatusCodes.Status400BadRequest, exception.Message);
        }
    }

    private static async Task<IResult> TestSavedPoint(
        string sourceId, string pointId, PointTestRequest request,
        IPointSourceService sources, IPointMappingResolver resolver,
        IPointMappingExecutionService execution, IPointValueConverter values,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(request.PointId)
                && !string.Equals(pointId, request.PointId, StringComparison.Ordinal))
            {
                return Error(400, "pointId must match request path");
            }

            var source = await sources.GetAsync(sourceId, cancellationToken);

            return await ExecutePointTest(source, pointId, request.Operation, request.Value,
                resolver, execution, values, cancellationToken);
        }
        catch (PointSourceNotFoundException)
        {
            return Error(404, "point source not found");
        }
        catch (ArgumentException exception)
        {
            return Error(400, exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return Error(400, exception.Message);
        }
    }

    private static async Task<IResult> ExecutePointTest(
        PointSource source, string pointId, string operation, System.Text.Json.Nodes.JsonNode? value,
        IPointMappingResolver resolver, IPointMappingExecutionService execution,
        IPointValueConverter values, CancellationToken cancellationToken)
    {
        PointMappingResolution resolution;

        try
        {
            resolution = resolver.Resolve(source, pointId);
        }
        catch (ArgumentException)
        {
            return Error(404, "point not found in point source");
        }

        if (operation == "read" && resolution.Point.Readable)
        {
            var result = await execution.ReadAsync(resolution, cancellationToken);

            if (result.Quality != DataQualityType.Good)
            {
                return Error(502, PointTestDiagnostic(
                    result.Diagnostic ?? "mapping read failed",
                    result.Response));
            }

            result.Values.TryGetValue(resolution.Alias, out var raw);
            var converted = values.Parse(resolution.Point, raw);

            return Results.Json(new PointTestResult(source.Id, pointId, resolution.Mapping.Id,
                resolution.Alias, operation, converted.Value, converted.Quality, null,
                converted.Diagnostic ?? result.Diagnostic, result.Response));
        }

        if (operation == "command" && resolution.Point.Commandable)
        {
            var result = await execution.CommandAsync(resolution, value, cancellationToken);

            if (result.Diagnostic is not null)
            {
                return Error(502, PointTestDiagnostic(result.Diagnostic, result.Response));
            }

            return Results.Json(new PointTestResult(source.Id, pointId, resolution.Mapping.Id,
                resolution.Alias, operation, value?.DeepClone(), DataQualityType.Good,
                result.RenderedRequest, null, result.Response));
        }

        return Error(400, "point does not support the requested operation");
    }

    private static string PointTestDiagnostic(
        string diagnostic,
        HttpResponsePreview? response)
    {
        if (response?.RequestUri is null
            || diagnostic.Contains(response.RequestUri, StringComparison.Ordinal))
        {
            return diagnostic;
        }

        return $"{diagnostic} ({response.RequestMethod ?? "HTTP"} {response.RequestUri})";
    }

    private static PointSourceSummary Summary(PointSource source) => new(
        source.Id, source.Name, source.Description, source.Enabled, source.Kind,
        source.Mappings.Count, source.Points.Count, source.Revision, source.UpdatedAt);

    private static async Task<IResult> WriteSource(
        HttpResponse response,
        Func<Task<PointSource>> operation,
        int status)
    {
        try
        {
            var source = await operation();
            response.Headers.ETag = source.Revision.ToString(CultureInfo.InvariantCulture);

            return Results.Text(
                PointSourceYaml.Render(source),
                "application/yaml",
                Encoding.UTF8,
                status);
        }
        catch (PointSourceNotFoundException)
        {
            return Error(StatusCodes.Status404NotFound, "point source not found");
        }
        catch (PointSourceConflictException exception)
        {
            return Error(StatusCodes.Status409Conflict, exception.Message);
        }
        catch (PointSourceValidationException exception)
        {
            return Error(StatusCodes.Status400BadRequest, exception.Message);
        }
        catch (PointDefinitionValidationException exception)
        {
            return Error(StatusCodes.Status400BadRequest, exception.Message);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Error(
                StatusCodes.Status500InternalServerError,
                "unable to persist point source");
        }
    }

    private static async Task<(PointSource? Source, IResult? Error)> Decode(
        HttpRequest request,
        CancellationToken cancellationToken)
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
                return (
                    null,
                    Error(StatusCodes.Status400BadRequest, "unable to read YAML request"));
            }

            return (PointSourceYaml.Parse(buffer.AsSpan(0, length)), null);
        }
        catch (ConfigurationYamlException exception)
        {
            return (null, Error(StatusCodes.Status400BadRequest, exception.Message));
        }
    }

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

    private static IResult Error(int status, string message) =>
        Results.Json(new ErrorResponse(message), statusCode: status);

    private static string ClientKey(HttpRequest request) =>
        request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
}