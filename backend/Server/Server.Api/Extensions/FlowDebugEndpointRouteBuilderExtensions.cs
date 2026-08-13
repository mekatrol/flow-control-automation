using Server.Api.Contracts;
using Server.Services;
using Server.Services.Contracts;
using System.Text;
using System.Text.Json;

namespace Server.Api.Extensions;

public static class FlowDebugEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapFlowDebugEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/flows/{flowId}/debug-sessions", Start);
        endpoints.MapGet("/api/flows/{flowId}/debug-sessions/{sessionId}", Get);
        endpoints.MapPost("/api/flows/{flowId}/debug-sessions/{sessionId}/step", Step);
        endpoints.MapPost("/api/flows/{flowId}/debug-sessions/{sessionId}/step-instruction", StepInstruction);
        endpoints.MapPost("/api/flows/{flowId}/debug-sessions/{sessionId}/step-node", StepNode);
        endpoints.MapPost("/api/flows/{flowId}/debug-sessions/{sessionId}/run-to", RunTo);
        endpoints.MapPut("/api/flows/{flowId}/debug-sessions/{sessionId}/breakpoints", ReplaceBreakpoints);
        endpoints.MapGet("/api/flows/{flowId}/debug-sessions/{sessionId}/frame", Inspect);
        endpoints.MapPost("/api/flows/{flowId}/debug-sessions/{sessionId}/restart", Restart);
        endpoints.MapPost("/api/flows/{flowId}/debug-sessions/{sessionId}/run", Run);
        endpoints.MapPost("/api/flows/{flowId}/debug-sessions/{sessionId}/pause", Pause);
        endpoints.MapPost("/api/flows/{flowId}/debug-sessions/{sessionId}/live-output", EnableLiveOutput);
        endpoints.MapPost("/api/flows/{flowId}/debug-sessions/{sessionId}/stop", Stop);
        endpoints.MapGet("/api/flows/{flowId}/debug-sessions/{sessionId}/events", Events);
        return endpoints;
    }

    private static async Task<IResult> Start(
        string flowId,
        CreateDebugSessionRequest request,
        IFlowDebugService debug,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(flowId, request.Source.Id, StringComparison.Ordinal))
        {
            return Results.BadRequest(new ErrorResponse("flow ID must match the request path"));
        }
        try
        {
            var session = await debug.StartAsync(
                new StartFlowDebugSession(request.Source, request.Host, request.ReplaceExisting, request.EmulatorId),
                cancellationToken);
            return Results.Json(session, statusCode: StatusCodes.Status201Created);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return MapError(exception);
        }
    }

    private static async Task<IResult> StepInstruction(
        string flowId,
        string sessionId,
        IFlowDebugService debug,
        CancellationToken cancellationToken) => await MapDebugResult(
            () => debug.StepInstructionAsync(flowId, sessionId, cancellationToken));

    private static async Task<IResult> StepNode(
        string flowId,
        string sessionId,
        IFlowDebugService debug,
        CancellationToken cancellationToken) => await MapDebugResult(
            () => debug.StepNodeAsync(flowId, sessionId, cancellationToken));

    private static async Task<IResult> RunTo(
        string flowId,
        string sessionId,
        FlowDebugBreakpoint breakpoint,
        IFlowDebugService debug,
        CancellationToken cancellationToken) => await MapDebugResult(
            () => debug.RunToAsync(flowId, sessionId, breakpoint, cancellationToken));

    private static async Task<IResult> ReplaceBreakpoints(
        string flowId,
        string sessionId,
        IReadOnlyList<FlowDebugBreakpoint> breakpoints,
        IFlowDebugService debug,
        CancellationToken cancellationToken) => await MapDebugResult(
            () => debug.ReplaceBreakpointsAsync(flowId, sessionId, breakpoints, cancellationToken));

    private static async Task<IResult> Inspect(
        string flowId,
        string sessionId,
        IFlowDebugService debug,
        CancellationToken cancellationToken) => await MapDebugResult(
            () => debug.InspectAsync(flowId, sessionId, cancellationToken));

    private static async Task<IResult> Restart(
        string flowId,
        string sessionId,
        IFlowDebugService debug,
        CancellationToken cancellationToken) => await MapDebugResult(
            () => debug.RestartAsync(flowId, sessionId, cancellationToken));

    private static async Task<IResult> MapDebugResult<T>(Func<Task<T>> operation)
    {
        try
        {
            return Results.Json(await operation());
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return MapError(exception);
        }
    }

    private static async Task<IResult> Get(
        string flowId,
        string sessionId,
        IFlowDebugService debug,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Json(await debug.GetAsync(flowId, sessionId, cancellationToken));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return MapError(exception);
        }
    }

    private static async Task<IResult> Step(
        string flowId,
        string sessionId,
        IFlowDebugService debug,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Json(await debug.StepAsync(flowId, sessionId, cancellationToken));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return MapError(exception);
        }
    }

    private static async Task<IResult> Stop(
        string flowId,
        string sessionId,
        IFlowDebugService debug,
        CancellationToken cancellationToken)
    {
        try
        {
            await debug.StopAsync(flowId, sessionId, cancellationToken);
            return Results.NoContent();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return MapError(exception);
        }
    }

    private static async Task<IResult> Run(
        string flowId, string sessionId, RunDebugSessionRequest request, IFlowDebugService debug,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Json(await debug.RunAsync(flowId, sessionId, request.IntervalMilliseconds, cancellationToken));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return MapError(exception);
        }
    }

    private static async Task<IResult> Pause(
        string flowId, string sessionId, IFlowDebugService debug, CancellationToken cancellationToken)
    {
        try
        {
            return Results.Json(await debug.PauseAsync(flowId, sessionId, cancellationToken));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return MapError(exception);
        }
    }

    private static async Task<IResult> EnableLiveOutput(
        string flowId,
        string sessionId,
        EnableLiveOutputRequest request,
        IFlowDebugService debug,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Json(await debug.EnableLiveOutputAsync(
                flowId,
                sessionId,
                request.ConfirmedPointIds,
                cancellationToken));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return MapError(exception);
        }
    }

    private static async Task<IResult> Events(
        string flowId,
        string sessionId,
        IFlowDebugService debug,
        CancellationToken cancellationToken)
    {
        try
        {
            var initial = await debug.GetAsync(flowId, sessionId, cancellationToken);
            return Results.Stream(
                async stream =>
                {
                    var current = initial;
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var json = JsonSerializer.Serialize(current, FlowControlJson.Options);
                        var bytes = Encoding.UTF8.GetBytes($"event: status\ndata: {json}\n\n");
                        await stream.WriteAsync(bytes, cancellationToken);
                        await stream.FlushAsync(cancellationToken);
                        await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
                        try
                        {
                            current = await debug.GetAsync(flowId, sessionId, cancellationToken);
                        }
                        catch (FlowDebugSessionNotFoundException)
                        {
                            break;
                        }
                    }
                },
                "text/event-stream");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return MapError(exception);
        }
    }

    private static IResult MapError(Exception exception) => exception switch
    {
        FlowDebugSessionNotFoundException =>
            Results.NotFound(new ErrorResponse("debug session not found")),
        FlowCompilationException compilation =>
            Results.Json(compilation.Diagnostics, statusCode: StatusCodes.Status400BadRequest),
        ControllerGatewayException { Category: "busy" } gateway =>
            Results.Conflict(new ErrorResponse(gateway.Message)),
        ControllerGatewayException { Category: "validation" or "stale_session" } gateway =>
            Results.Json(new ErrorResponse(gateway.Message), statusCode: StatusCodes.Status422UnprocessableEntity),
        ControllerGatewayException gateway =>
            Results.Json(new ErrorResponse(gateway.Message), statusCode: StatusCodes.Status503ServiceUnavailable),
        _ => Results.Json(
            new ErrorResponse("debug service unavailable"),
            statusCode: StatusCodes.Status503ServiceUnavailable)
    };
}
