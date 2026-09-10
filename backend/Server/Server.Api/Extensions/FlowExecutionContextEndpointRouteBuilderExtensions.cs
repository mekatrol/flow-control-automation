#pragma warning disable IDE0011, CC0001, CC0002, CC0003
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Server.Api.Contracts;
using Server.Services;
using System.Text.Json;

namespace Server.Api.Extensions;

internal static class FlowExecutionContextEndpointRouteBuilderExtensions
{
    internal static IEndpointRouteBuilder MapFlowExecutionContextEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/flows/{flowId}/execution-contexts", Create);
        endpoints.MapGet("/api/execution-contexts/{contextId}", (string contextId, IFlowExecutionContextService service, CancellationToken token) => Map(() => service.GetAsync(contextId, token)));
        endpoints.MapDelete("/api/execution-contexts/{contextId}", (string contextId, IFlowExecutionContextService service, CancellationToken token) => Map(() => service.StopAsync(contextId, token)));
        endpoints.MapPost("/api/execution-contexts/{contextId}/run", (string contextId, RunFlowExecution request, IFlowExecutionContextService service, CancellationToken token) => Map(() => service.RunAsync(contextId, request, token)));
        endpoints.MapPost("/api/execution-contexts/{contextId}/pause", (string contextId, IFlowExecutionContextService service, CancellationToken token) => Map(() => service.PauseAsync(contextId, token)));
        endpoints.MapPost("/api/execution-contexts/{contextId}/stop", (string contextId, IFlowExecutionContextService service, CancellationToken token) => Map(() => service.StopAsync(contextId, token)));
        endpoints.MapPost("/api/execution-contexts/{contextId}/restart", (string contextId, IFlowExecutionContextService service, CancellationToken token) => Map(() => service.RestartAsync(contextId, token)));
        endpoints.MapPost("/api/execution-contexts/{contextId}/step-tick", (string contextId, IFlowExecutionContextService service, CancellationToken token) => Map(() => service.StepTickAsync(contextId, token)));
        endpoints.MapPost("/api/execution-contexts/{contextId}/step-node", (string contextId, IFlowExecutionContextService service, CancellationToken token) => Map(() => service.StepNodeAsync(contextId, token)));
        endpoints.MapPost("/api/execution-contexts/{contextId}/step-instruction", (string contextId, IFlowExecutionContextService service, CancellationToken token) => Map(() => service.StepInstructionAsync(contextId, token)));
        endpoints.MapPost("/api/execution-contexts/{contextId}/run-to", (string contextId, FlowDebugBreakpoint request, IFlowExecutionContextService service, CancellationToken token) => Map(() => service.RunToAsync(contextId, request, token)));
        endpoints.MapPut("/api/execution-contexts/{contextId}/breakpoints", (string contextId, IReadOnlyList<FlowDebugBreakpoint> request, IFlowExecutionContextService service, CancellationToken token) => Map(() => service.ReplaceBreakpointsAsync(contextId, request, token)));
        endpoints.MapPut("/api/execution-contexts/{contextId}/inputs", (string contextId, ApplyFlowExecutionInputs request, IFlowExecutionContextService service, CancellationToken token) => Map(() => service.ApplyInputsAsync(contextId, request, token)));
        endpoints.MapPost("/api/execution-contexts/{contextId}/advance", (string contextId, AdvanceFlowExecution request, IFlowExecutionContextService service, CancellationToken token) => Map(() => service.AdvanceAsync(contextId, request, token)));
        endpoints.MapPut("/api/execution-contexts/{contextId}/fault", (string contextId, InjectFlowExecutionFault request, IFlowExecutionContextService service, CancellationToken token) => Map(() => service.InjectFaultAsync(contextId, request, token)));
        endpoints.MapPost("/api/execution-contexts/{contextId}/reset-io", (string contextId, ResetFlowExecutionIo request, IFlowExecutionContextService service, CancellationToken token) => Map(() => service.ResetIoAsync(contextId, request, token)));
        endpoints.MapPost("/api/execution-contexts/{contextId}/reset-inputs", (string contextId, IFlowExecutionContextService service, CancellationToken token) => Map(() => service.ResetInputsAsync(contextId, token)));
        endpoints.MapPost("/api/execution-contexts/{contextId}/live-output", (string contextId, EnableFlowExecutionLiveOutput request, IFlowExecutionContextService service, CancellationToken token) => Map(() => service.EnableLiveOutputAsync(contextId, request, token)));
        endpoints.MapPost("/api/execution-contexts/{contextId}/keepalive", (string contextId, IFlowExecutionContextService service, CancellationToken token) => Map(() => service.KeepAliveAsync(contextId, token)));
        return endpoints;
    }

    private static async Task<IResult> Create(string flowId, HttpRequest http, IFlowExecutionContextService service, IOptions<JsonOptions> options, CancellationToken token)
    {
        try
        {
            var request = await JsonSerializer.DeserializeAsync<CreateFlowExecutionContext>(http.Body, options.Value.SerializerOptions, token);
            if (request is null || !string.Equals(flowId, request.FlowId, StringComparison.Ordinal)) return Error(400, "flow id must match the request path");
            return Results.Json(await service.CreateAsync(request, token), statusCode: 201);
        }
        catch (JsonException exception) { return Error(400, exception.Message); }
        catch (Exception exception) { return Failure(exception); }
    }

    private static async Task<IResult> Map(Func<Task<FlowExecutionContext>> action)
    {
        try { return Results.Json(await action()); }
        catch (Exception exception) { return Failure(exception); }
    }

    private static IResult Failure(Exception exception) => exception switch
    {
        FlowNotFoundException or FlowExecutionContextNotFoundException => Error(404, exception.Message),
        FlowExecutionContextConflictException => Error(409, exception.Message),
        FlowCompilationException compilation => Results.Json(new { code = "compilation_failed", message = "The saved flow could not be compiled.", details = compilation.Diagnostics }, statusCode: 422),
        FlowExecutionCapabilityException or ControllerGatewayException or FlowSimulatorException => Error(422, exception.Message),
        _ when exception is OperationCanceledException => Results.StatusCode(499),
        _ => Error(500, "Execution context operation failed.")
    };

    private static IResult Error(int status, string message) => Results.Json(new ErrorResponse(message), statusCode: status);
}