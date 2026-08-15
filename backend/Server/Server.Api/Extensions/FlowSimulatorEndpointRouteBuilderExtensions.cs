using Server.Api.Contracts;
using Server.Services;
using Server.Services.Contracts;

namespace Server.Api.Extensions;

public static class FlowSimulatorEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapFlowSimulatorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/flows/{flowId}/simulator-sessions", Start);
        endpoints.MapGet("/api/flows/{flowId}/simulator-sessions/{sessionId}", Get);
        endpoints.MapPost("/api/flows/{flowId}/simulator-sessions/{sessionId}/step", Step);
        endpoints.MapPost("/api/flows/{flowId}/simulator-sessions/{sessionId}/apply-and-step", ApplyInputsAndStep);
        endpoints.MapPost("/api/flows/{flowId}/simulator-sessions/{sessionId}/advance", Advance);
        endpoints.MapPut("/api/flows/{flowId}/simulator-sessions/{sessionId}/fault", InjectFault);
        endpoints.MapPost("/api/flows/{flowId}/simulator-sessions/{sessionId}/reset-io", ResetIo);
        endpoints.MapPost("/api/flows/{flowId}/simulator-sessions/{sessionId}/reset-inputs", ResetInputs);
        endpoints.MapPost("/api/flows/{flowId}/simulator-sessions/{sessionId}/step-node", StepNode);
        endpoints.MapPost("/api/flows/{flowId}/simulator-sessions/{sessionId}/step-instruction", StepInstruction);
        endpoints.MapPost("/api/flows/{flowId}/simulator-sessions/{sessionId}/restart", Restart);
        endpoints.MapPost("/api/flows/{flowId}/simulator-sessions/{sessionId}/run", Run);
        endpoints.MapPost("/api/flows/{flowId}/simulator-sessions/{sessionId}/pause", Pause);
        endpoints.MapDelete("/api/flows/{flowId}/simulator-sessions/{sessionId}", Stop);
        return endpoints;
    }

    private static async Task<IResult> Start(string flowId, CreateSimulatorSessionRequest request, IFlowSimulatorService simulator, CancellationToken cancellationToken)
    {
        if (!string.Equals(flowId, request.Source.Id, StringComparison.Ordinal))
        {
            return Error(StatusCodes.Status400BadRequest, "compile_invalid_source", "Flow ID must match the request path.", "/source/id");
        }

        return await Map(() => simulator.StartAsync(request.Source, request.ReplaceExisting, cancellationToken), StatusCodes.Status201Created);
    }

    private static async Task<IResult> Get(string flowId, string sessionId, IFlowSimulatorService simulator, CancellationToken cancellationToken) =>
        await Map(() => simulator.GetAsync(flowId, sessionId, cancellationToken));
    private static async Task<IResult> Step(string flowId, string sessionId, IFlowSimulatorService simulator, CancellationToken cancellationToken) =>
        await Map(() => simulator.StepTickAsync(flowId, sessionId, cancellationToken));
    private static async Task<IResult> ApplyInputsAndStep(string flowId, string sessionId, SetEmulatorInputsRequest request, IFlowSimulatorService simulator, CancellationToken cancellationToken) =>
        await Map(() => simulator.ApplyInputsAndStepAsync(flowId, sessionId, request.Inputs, cancellationToken));
    private static async Task<IResult> Advance(string flowId, string sessionId, AdvanceEmulatorRequest request, IFlowSimulatorService simulator, CancellationToken cancellationToken) =>
        await Map(() => simulator.AdvanceAsync(flowId, sessionId, request.Milliseconds, cancellationToken));
    private static async Task<IResult> InjectFault(string flowId, string sessionId, InjectEmulatorFaultRequest request, IFlowSimulatorService simulator, CancellationToken cancellationToken) =>
        await Map(() => simulator.InjectFaultAsync(flowId, sessionId, request.Fault, cancellationToken));
    private static async Task<IResult> ResetIo(string flowId, string sessionId, ResetEmulatorRequest request, IFlowSimulatorService simulator, CancellationToken cancellationToken) =>
        await Map(() => simulator.ResetIoAsync(flowId, sessionId, request.PowerCycle, cancellationToken));
    private static async Task<IResult> ResetInputs(string flowId, string sessionId, IFlowSimulatorService simulator, CancellationToken cancellationToken) =>
        await Map(() => simulator.ResetInputsAsync(flowId, sessionId, cancellationToken));
    private static async Task<IResult> StepNode(string flowId, string sessionId, IFlowSimulatorService simulator, CancellationToken cancellationToken) =>
        await Map(() => simulator.StepNodeAsync(flowId, sessionId, cancellationToken));
    private static async Task<IResult> StepInstruction(string flowId, string sessionId, IFlowSimulatorService simulator, CancellationToken cancellationToken) =>
        await Map(() => simulator.StepInstructionAsync(flowId, sessionId, cancellationToken));
    private static async Task<IResult> Restart(string flowId, string sessionId, IFlowSimulatorService simulator, CancellationToken cancellationToken) =>
        await Map(() => simulator.RestartAsync(flowId, sessionId, cancellationToken));
    private static async Task<IResult> Run(string flowId, string sessionId, RunDebugSessionRequest request, IFlowSimulatorService simulator, CancellationToken cancellationToken) =>
        await Map(() => simulator.RunAsync(flowId, sessionId, request.IntervalMilliseconds, cancellationToken));
    private static async Task<IResult> Pause(string flowId, string sessionId, IFlowSimulatorService simulator, CancellationToken cancellationToken) =>
        await Map(() => simulator.PauseAsync(flowId, sessionId, cancellationToken));

    private static async Task<IResult> Stop(string flowId, string sessionId, IFlowSimulatorService simulator, CancellationToken cancellationToken)
    {
        try
        {
            await simulator.StopAsync(flowId, sessionId, cancellationToken);
            return Results.NoContent();
        }
        catch (Exception exception) when (exception is not OperationCanceledException) { return MapError(exception); }
    }

    private static async Task<IResult> Map(Func<Task<FlowSimulatorSession>> operation, int status = StatusCodes.Status200OK)
    {
        try { return Results.Json(await operation(), statusCode: status); }
        catch (Exception exception) when (exception is not OperationCanceledException) { return MapError(exception); }
    }

    private static IResult MapError(Exception exception) => exception switch
    {
        FlowSimulatorException { Code: "simulator_session_not_found" } error => Error(StatusCodes.Status404NotFound, error.Code, error.Message),
        FlowSimulatorException { Code: "simulator_session_conflict" } error => Error(StatusCodes.Status409Conflict, error.Code, error.Message),
        FlowSimulatorException { Code: "simulator_limit_exceeded" } error => Error(StatusCodes.Status429TooManyRequests, error.Code, error.Message),
        FlowCompilationException compilation => Results.Json(new
        {
            code = "compile_invalid_source",
            message = "The draft flow could not be compiled.",
            diagnostics = compilation.Diagnostics
        }, statusCode: StatusCodes.Status422UnprocessableEntity),
        ControllerGatewayException { Category: "validation" } error => Error(StatusCodes.Status422UnprocessableEntity, "simulator_capability_unsupported", error.Message),
        FlowVirtualMachineException error => Error(StatusCodes.Status422UnprocessableEntity, "simulator_vm_fault", error.Message),
        _ => Error(StatusCodes.Status503ServiceUnavailable, "simulator_unavailable", "The simulator is unavailable.")
    };

    private static IResult Error(int status, string code, string message, string? path = null) =>
        Results.Json(new SimulatorErrorResponse(code, message, path), statusCode: status);
}