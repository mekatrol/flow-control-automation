using Server.Api.Contracts;
using Server.Common.Models.Communication;
using Server.Services;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

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
        endpoints.MapPost("/api/point-sources/test-point", TestPoint);
        endpoints.MapPost("/api/point-sources/{sourceId}/test", TestSaved);

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
            return Results.Json(await sources.ListAsync(
                new PointSourceListOptions(
                    query["filter"].ToString(),
                    page,
                    pageSize,
                    sort),
                cancellationToken));
        }
        catch (PointSourceValidationException exception)
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

    private static async Task<IResult> TestPoint(
        PointTestRequest request,
        IPointSourceValidator sourceValidator,
        IPointDefinitionValidator pointValidator,
        IDnsLookup dns,
        ICredentialResolver credentials,
        IHttpProtocolCheck http,
        CancellationToken cancellationToken)
    {
        try
        {
            var source = PointSourceYaml.Parse(Encoding.UTF8.GetBytes(request.SourceYaml));
            var point = PointYaml.Parse(Encoding.UTF8.GetBytes(request.PointYaml));
            sourceValidator.Validate(source);
            var validated = pointValidator.Validate(
                point,
                new PointValidationContext(new Dictionary<string, PointSource>(StringComparer.Ordinal)
                {
                    [source.Id] = source
                }));

            if (source.Kind != PointSourceKind.HttpJson || validated.Mapping is not HttpJsonPointMapping mapping)
            {
                return Error(StatusCodes.Status400BadRequest, "point testing requires an HTTP/JSON mapping");
            }

            var operation = request.Operation;

            if (operation is not ("read" or "write")
                || operation == "read" && !point.Readable
                || operation == "write" && !point.Commandable)
            {
                return Error(StatusCodes.Status400BadRequest, "point does not support the requested operation");
            }

            var endpoint = new Uri(new Uri(source.Connection.BaseUrl!), mapping.Path);
            var addresses = await dns.LookupAsync(endpoint.Host, cancellationToken);

            if (addresses.Count == 0 || addresses.Any(address => ConnectivityPolicy.IsForbidden(
                address,
                source.Connection.AllowPrivateNetwork == true)))
            {
                return Error(StatusCodes.Status400BadRequest, "HTTP/JSON destination is forbidden or unavailable");
            }

            var credential = await credentials.ResolveAsync(
                source.CredentialRef ?? string.Empty,
                cancellationToken);
            var protocolResult = operation == "read"
                ? await http.ReadAsync(source, endpoint, credential, addresses, cancellationToken)
                : await http.WriteAsync(
                    source,
                    endpoint,
                    mapping.Method,
                    CommandBody(request.Value, mapping.ValuePointer, mapping.ContentType),
                    mapping.ContentType,
                    credential,
                    addresses,
                    cancellationToken);

            if (protocolResult.Response is null)
            {
                return Error(
                    StatusCodes.Status502BadGateway,
                    protocolResult.Diagnostic ?? "HTTP/JSON response was unavailable");
            }

            var value = operation == "read"
                ? SelectValue(protocolResult.Response.Body, mapping.JsonPointer)
                : request.Value?.DeepClone();

            return Results.Json(new PointTestResult(
                operation,
                value,
                protocolResult.Diagnostic,
                protocolResult.Response));
        }
        catch (Exception exception) when (exception is ConfigurationYamlException
            or PointSourceValidationException
            or PointDefinitionValidationException
            or UriFormatException
            or InvalidOperationException)
        {
            return Error(StatusCodes.Status400BadRequest, exception.Message);
        }
    }

    private static string CommandBody(JsonNode? value, string? pointer, string contentType)
    {
        if (contentType == "application/x-www-form-urlencoded")
        {
            var name = pointer![1..].Replace("~1", "/").Replace("~0", "~");
            var formValue = value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text)
                ? text
                : value?.ToJsonString() ?? string.Empty;

            return $"{Uri.EscapeDataString(name)}={Uri.EscapeDataString(formValue)}";
        }

        if (string.IsNullOrEmpty(pointer))
        {
            return value?.ToJsonString() ?? "null";
        }

        if (!pointer.StartsWith('/'))
        {
            throw new InvalidOperationException("mapping.valuePointer must be a JSON Pointer starting with /");
        }

        JsonNode root = new JsonObject();
        var segments = pointer.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => segment.Replace("~1", "/").Replace("~0", "~"))
            .ToArray();
        var current = (JsonObject)root;

        for (var index = 0; index < segments.Length - 1; index++)
        {
            var child = new JsonObject();
            current[segments[index]] = child;
            current = child;
        }

        current[segments[^1]] = value?.DeepClone();

        return root.ToJsonString();
    }

    private static JsonNode? SelectValue(string body, string? pointer)
    {
        var value = JsonNode.Parse(body);

        if (string.IsNullOrEmpty(pointer))
        {
            return value;
        }

        foreach (var rawSegment in pointer.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var segment = rawSegment.Replace("~1", "/").Replace("~0", "~");
            value = value is JsonArray array && int.TryParse(segment, out var index)
                ? array[index]
                : value?[segment];
        }

        return value?.DeepClone()
            ?? throw new InvalidOperationException($"JSON pointer '{pointer}' did not select a value");
    }

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