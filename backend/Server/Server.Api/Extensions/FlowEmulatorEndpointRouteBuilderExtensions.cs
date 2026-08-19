using Server.Api.Contracts;
using Server.Services;

namespace Server.Api.Extensions;

public static class FlowEmulatorEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapFlowEmulatorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/emulators", Create);
        endpoints.MapGet("/api/emulators/{emulatorId}", Get);
        endpoints.MapPut("/api/emulators/{emulatorId}/inputs", SetInputs);
        endpoints.MapPost("/api/emulators/{emulatorId}/apply-and-step", ApplyInputsAndStep);
        endpoints.MapPost("/api/emulators/{emulatorId}/advance", Advance);
        endpoints.MapPut("/api/emulators/{emulatorId}/fault", InjectFault);
        endpoints.MapPost("/api/emulators/{emulatorId}/reset", Reset);
        endpoints.MapPost("/api/emulators/{emulatorId}/reset-inputs", ResetInputs);
        endpoints.MapDelete("/api/emulators/{emulatorId}", Delete);
        return endpoints;
    }

    private static async Task<IResult> Create(
        CreateFlowEmulatorRequest request,
        IFlowEmulatorService emulators,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Json(
                await emulators.CreateAsync(request.Source, cancellationToken),
                statusCode: StatusCodes.Status201Created);
        }
        catch (FlowCompilationException exception)
        {
            return Results.Json(exception.Diagnostics, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }

    private static IResult Get(string emulatorId, IFlowEmulatorService emulators) =>
        Map(() => emulators.Get(emulatorId));

    private static IResult SetInputs(
        string emulatorId,
        SetEmulatorInputsRequest request,
        IFlowEmulatorService emulators) => Map(() => emulators.SetInputs(emulatorId, request.Inputs));

    private static IResult ApplyInputsAndStep(
        string emulatorId,
        SetEmulatorInputsRequest request,
        IFlowEmulatorService emulators) =>
        Map(() => emulators.ApplyInputsAndStep(emulatorId, request.Inputs));

    private static IResult Advance(
        string emulatorId,
        AdvanceEmulatorRequest request,
        IFlowEmulatorService emulators) => Map(() => emulators.Advance(emulatorId, request.Milliseconds, request.Scan));

    private static IResult InjectFault(
        string emulatorId,
        InjectEmulatorFaultRequest request,
        IFlowEmulatorService emulators) => Map(() => emulators.InjectFault(emulatorId, request.Fault));

    private static IResult Reset(
        string emulatorId,
        ResetEmulatorRequest request,
        IFlowEmulatorService emulators) => Map(() => emulators.Reset(emulatorId, request.PowerCycle));

    private static IResult ResetInputs(string emulatorId, IFlowEmulatorService emulators) =>
        Map(() => emulators.ResetInputs(emulatorId));

    private static IResult Delete(string emulatorId, IFlowEmulatorService emulators)
    {
        emulators.Delete(emulatorId);
        return Results.NoContent();
    }

    private static IResult Map<T>(Func<T> action)
    {
        try
        {
            return Results.Json(action());
        }
        catch (FlowEmulatorNotFoundException)
        {
            return Results.NotFound();
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new ErrorResponse(exception.Message));
        }
        catch (FlowVmException exception)
        {
            return Results.UnprocessableEntity(new ErrorResponse(exception.Message));
        }
    }
}