using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Server.Api.Contracts;
using Server.Services;
using Server.Services.Contracts;
using System.Text.Json;

namespace Server.Api.Extensions;

public static class EndpointRouteBuilderExtensions
{
    public const string HealthRoute = "/api/health";
    private const long MaximumFlowRequestBytes = 10L * 1024 * 1024;

    public static IEndpointRouteBuilder MapFlowControlEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(HealthRoute, static () => Results.Json(new { status = "ok" }));
        endpoints.MapGet("/api/flows", ListFlows);
        endpoints.MapPost("/api/flows", CreateFlow);
        endpoints.MapGet("/api/flows/{flowId}", GetFlow);
        endpoints.MapPut("/api/flows/{flowId}", SaveFlow);
        endpoints.MapDelete("/api/flows/{flowId}", DeleteFlow);
        endpoints.MapPost("/api/flows/{flowId}/deploy", DeployFlow);
        endpoints.MapPost("/api/flows/{flowId}/disable", DisableFlow);
        endpoints.MapPost("/api/flows/{flowId}/enable", EnableFlow);
        endpoints.MapGet("/api/flows/{flowId}/runtime", GetRuntime);
        endpoints.MapPointSourceEndpoints();
        return endpoints;
    }

    private static async Task<IResult> ListFlows(
        HttpRequest request,
        IFlowService flows,
        CancellationToken cancellationToken)
    {
        var query = request.Query;
        if (!PositiveInteger(query["page"].ToString(), 1, out var page))
        {
            return Error(StatusCodes.Status400BadRequest, "page must be a positive integer");
        }

        if (!PositiveInteger(query["pageSize"].ToString(), 10, out var pageSize)
            || pageSize is not (10 or 20 or 50))
        {
            return Error(StatusCodes.Status400BadRequest, "pageSize must be 10, 20, or 50");
        }

        var sort = query["sort"].ToString();
        sort = sort.Length == 0 ? "ascending" : sort;
        if (sort is not ("ascending" or "descending"))
        {
            return Error(StatusCodes.Status400BadRequest, "sort must be ascending or descending");
        }

        var statuses = query["status"]
            .Where(static status => status is not null)
            .Select(static status => status!)
            .ToArray();
        if (statuses.Any(status => status is not ("draft" or "deployed")))
        {
            return Error(
                StatusCodes.Status400BadRequest,
                "each status must be draft or deployed");
        }

        try
        {
            var result = await flows.ListAsync(
                new FlowListOptions(
                    query["filter"].ToString(),
                    statuses,
                    page,
                    pageSize,
                    sort),
                cancellationToken);
            return Results.Json(result);
        }
        catch (FlowValidationException exception)
        {
            return Error(StatusCodes.Status400BadRequest, exception.Message);
        }
    }

    private static async Task<IResult> CreateFlow(
        HttpRequest request,
        IFlowService flows,
        IOptions<JsonOptions> jsonOptions,
        CancellationToken cancellationToken)
    {
        var decoded = await DecodeAsync<CreateFlowRequest>(
            request,
            jsonOptions.Value.SerializerOptions,
            cancellationToken);
        if (decoded.Error is not null)
        {
            return decoded.Error;
        }

        if (string.IsNullOrWhiteSpace(decoded.Value!.Name))
        {
            return Error(StatusCodes.Status400BadRequest, "name must be non-empty");
        }

        return await MapFlowResult(
            () => flows.CreateAsync(decoded.Value.Name, cancellationToken),
            StatusCodes.Status201Created);
    }

    private static async Task<IResult> GetFlow(
        string flowId,
        IFlowService flows,
        CancellationToken cancellationToken) =>
        await MapFlowResult(() => flows.GetAsync(flowId, cancellationToken));

    private static async Task<IResult> SaveFlow(
        string flowId,
        HttpRequest request,
        IFlowService flows,
        IOptions<JsonOptions> jsonOptions,
        CancellationToken cancellationToken)
    {
        var decoded = await DecodeAsync<Flow>(
            request,
            jsonOptions.Value.SerializerOptions,
            cancellationToken);
        return decoded.Error
            ?? await MapFlowResult(
                () => flows.SaveAsync(flowId, decoded.Value!, cancellationToken));
    }

    private static async Task<IResult> DeleteFlow(
        string flowId,
        IFlowService flows,
        IFlowRuntimeService runtime,
        CancellationToken cancellationToken)
    {
        try
        {
            await flows.DeleteAsync(flowId, cancellationToken);
            runtime.Delete(flowId);
            return Results.NoContent();
        }
        catch (FlowNotFoundException)
        {
            return Error(StatusCodes.Status404NotFound, "flow not found");
        }
        catch (FlowConcurrencyException)
        {
            return Error(StatusCodes.Status409Conflict, "flow was changed by another request");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Error(StatusCodes.Status500InternalServerError, "unable to persist flow");
        }
    }

    private static async Task<IResult> DeployFlow(
        string flowId,
        IFlowService flows,
        IFlowRuntimeService runtime,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Json(runtime.Deploy(await flows.GetAsync(flowId, cancellationToken)));
        }
        catch (FlowNotFoundException)
        {
            return Error(StatusCodes.Status404NotFound, "flow not found");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Error(StatusCodes.Status503ServiceUnavailable, "runtime service unavailable");
        }
    }

    private static async Task<IResult> DisableFlow(
        string flowId,
        IFlowService flows,
        IFlowRuntimeService runtime,
        CancellationToken cancellationToken) =>
        await SetDisabled(flowId, disabled: true, flows, runtime, cancellationToken);

    private static async Task<IResult> EnableFlow(
        string flowId,
        IFlowService flows,
        IFlowRuntimeService runtime,
        CancellationToken cancellationToken) =>
        await SetDisabled(flowId, disabled: false, flows, runtime, cancellationToken);

    private static async Task<IResult> SetDisabled(
        string flowId,
        bool disabled,
        IFlowService flows,
        IFlowRuntimeService runtime,
        CancellationToken cancellationToken)
    {
        var result = await MapFlowResult(
            () => flows.SetDisabledAsync(flowId, disabled, cancellationToken));
        if (disabled && result is IValueHttpResult<Flow> { Value: not null } value)
        {
            runtime.Stop(value.Value);
        }

        return result;
    }

    private static async Task<IResult> GetRuntime(
        string flowId,
        IFlowService flows,
        IFlowRuntimeService runtime,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Json(runtime.Get(await flows.GetAsync(flowId, cancellationToken)));
        }
        catch (FlowNotFoundException)
        {
            return Error(StatusCodes.Status404NotFound, "flow not found");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Error(StatusCodes.Status503ServiceUnavailable, "runtime service unavailable");
        }
    }

    private static async Task<IResult> MapFlowResult(
        Func<Task<Flow>> operation,
        int successStatus = StatusCodes.Status200OK)
    {
        try
        {
            return Results.Json(await operation(), statusCode: successStatus);
        }
        catch (FlowNotFoundException)
        {
            return Error(StatusCodes.Status404NotFound, "flow not found");
        }
        catch (FlowValidationException exception)
        {
            return Error(StatusCodes.Status400BadRequest, exception.Message);
        }
        catch (FlowConcurrencyException)
        {
            return Error(StatusCodes.Status409Conflict, "flow was changed by another request");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Error(StatusCodes.Status500InternalServerError, "unable to persist flow");
        }
    }

    private static async Task<(T? Value, IResult? Error)> DecodeAsync<T>(
        HttpRequest request,
        JsonSerializerOptions options,
        CancellationToken cancellationToken)
    {
        if (request.ContentLength > MaximumFlowRequestBytes)
        {
            return (default, Error(StatusCodes.Status400BadRequest, "http: request body too large"));
        }

        try
        {
            request.HttpContext.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>()
                ?.MaxRequestBodySize = MaximumFlowRequestBytes;
            var value = await JsonSerializer.DeserializeAsync<T>(
                request.Body,
                options,
                cancellationToken);
            return value is null
                ? (default, Error(StatusCodes.Status400BadRequest, "request body must contain JSON"))
                : (value, null);
        }
        catch (JsonException exception)
        {
            return (default, Error(StatusCodes.Status400BadRequest, exception.Message));
        }
        catch (BadHttpRequestException exception)
        {
            return (default, Error(StatusCodes.Status400BadRequest, exception.Message));
        }
    }

    private static bool PositiveInteger(string value, int fallback, out int result)
    {
        if (value.Length == 0)
        {
            result = fallback;
            return true;
        }

        return int.TryParse(value, out result) && result > 0;
    }

    private static IResult Error(int status, string message) =>
        Results.Json(new ErrorResponse(message), statusCode: status);
}