using Server.Api.Contracts;
using Server.Services;
using Server.Services.Contracts;

namespace Server.Api.Extensions;

public static class FlowScenarioEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapFlowScenarioEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/flows/{flowId}/scenarios", List);
        endpoints.MapGet("/api/flows/{flowId}/scenarios/{scenarioId}", Get);
        endpoints.MapPut("/api/flows/{flowId}/scenarios/{scenarioId}", Save);
        endpoints.MapDelete("/api/flows/{flowId}/scenarios/{scenarioId}", Delete);
        endpoints.MapPost("/api/flows/{flowId}/scenarios/run", Run);
        return endpoints;
    }

    private static async Task<IResult> List(string flowId, IFlowScenarioService scenarios, CancellationToken cancellationToken) =>
        Results.Ok(await scenarios.ListAsync(flowId, cancellationToken));

    private static async Task<IResult> Get(string flowId, string scenarioId, IFlowScenarioService scenarios, CancellationToken cancellationToken) =>
        await Map(async () => Results.Ok(await scenarios.GetAsync(flowId, scenarioId, cancellationToken)));

    private static async Task<IResult> Save(string flowId, string scenarioId, FlowScenario scenario, IFlowScenarioService scenarios, CancellationToken cancellationToken) =>
        await Map(async () =>
        {
            if (scenario.FlowId != flowId || scenario.Id != scenarioId)
                throw new FlowScenarioException("scenario_invalid", "Flow and scenario IDs must match the request path.");
            return Results.Ok(await scenarios.SaveAsync(scenario, cancellationToken));
        });

    private static async Task<IResult> Delete(string flowId, string scenarioId, IFlowScenarioService scenarios, CancellationToken cancellationToken) =>
        await Map(async () =>
        {
            await scenarios.DeleteAsync(flowId, scenarioId, cancellationToken);
            return Results.NoContent();
        });

    private static async Task<IResult> Run(string flowId, RunFlowScenarioRequest request, IFlowScenarioService scenarios, CancellationToken cancellationToken) =>
        await Map(async () =>
        {
            if (request.Scenario.FlowId != flowId || request.Source.Id != flowId)
                throw new FlowScenarioException("scenario_invalid", "Flow IDs must match the request path.");
            return Results.Ok(await scenarios.RunAsync(request.Scenario, request.Source, cancellationToken));
        });

    private static async Task<IResult> Map(Func<Task<IResult>> operation)
    {
        try { return await operation(); }
        catch (FlowScenarioException error)
        {
            var status = error.Code switch
            {
                "scenario_not_found" => StatusCodes.Status404NotFound,
                "scenario_id_conflict" or "scenario_stale_revision" => StatusCodes.Status409Conflict,
                "scenario_limit_exceeded" => StatusCodes.Status413PayloadTooLarge,
                _ => StatusCodes.Status422UnprocessableEntity
            };
            return Results.Json(new SimulatorErrorResponse(error.Code, error.Message, error.Path), statusCode: status);
        }
    }
}
