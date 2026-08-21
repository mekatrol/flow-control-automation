using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Server.Api.Contracts;
using Server.Common.Contracts;
using Server.Services;

namespace Server.Api.Extensions;

public static class ExecutionConfigurationEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapExecutionConfigurationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/execution-contexts", async (IExecutionConfigurationService service, CancellationToken token) => Results.Json(await service.ListContextsAsync(token)));
        endpoints.MapGet("/api/execution-contexts/{id}", async (string id, IExecutionConfigurationService service, CancellationToken token) => await Map(() => service.GetContextAsync(id, token)));
        endpoints.MapPost("/api/execution-contexts", async (HttpRequest request, IExecutionConfigurationService service, IOptions<JsonOptions> options, CancellationToken token) => await DecodeAndMap<ExecutionContextDefinition>(request, options, value => service.SaveContextAsync(value, true, token), 201, token));
        endpoints.MapPut("/api/execution-contexts/{id}", async (string id, HttpRequest request, IExecutionConfigurationService service, IOptions<JsonOptions> options, CancellationToken token) => await DecodeAndMap<ExecutionContextDefinition>(request, options, value => EnsureId(id, value.Id, () => service.SaveContextAsync(value, false, token)), 200, token));
        endpoints.MapDelete("/api/execution-contexts/{id}", async (string id, IExecutionConfigurationService service, CancellationToken token) => await MapDelete(() => service.DeleteContextAsync(id, token)));

        endpoints.MapGet("/api/execution-instances", async (IExecutionConfigurationService service, CancellationToken token) => Results.Json(await service.ListInstancesAsync(token)));
        endpoints.MapGet("/api/execution-instances/{id}", async (string id, IExecutionConfigurationService service, CancellationToken token) => await Map(() => service.GetInstanceAsync(id, token)));
        endpoints.MapPost("/api/execution-instances", async (HttpRequest request, IExecutionConfigurationService service, IOptions<JsonOptions> options, CancellationToken token) => await DecodeAndMap<ExecutionInstance>(request, options, value => service.SaveInstanceAsync(value, true, token), 201, token));
        endpoints.MapPut("/api/execution-instances/{id}", async (string id, HttpRequest request, IExecutionConfigurationService service, IOptions<JsonOptions> options, CancellationToken token) => await DecodeAndMap<ExecutionInstance>(request, options, value => EnsureId(id, value.Id, () => service.SaveInstanceAsync(value, false, token)), 200, token));
        endpoints.MapDelete("/api/execution-instances/{id}", async (string id, IExecutionConfigurationService service, CancellationToken token) => await MapDelete(() => service.DeleteInstanceAsync(id, token)));
        endpoints.MapGet("/api/execution-instances/{id}/virtual-points", async (string id, IExecutionConfigurationService service, CancellationToken token) => await Map(() => service.ListAllocationsAsync(id, token)));
        endpoints.MapGet("/api/execution-instances/{id}/virtual-points/runtime", (string id, IVirtualPointRuntimeStore store) => Results.Json(store.List(id)));
        endpoints.MapGet("/api/execution-instances/{id}/virtual-points/{pointKey}/runtime", (string id, string pointKey, IVirtualPointRuntimeStore store) =>
            store.TrySnapshot(id, pointKey, out var value) ? Results.Json(value) : Results.NotFound(new { error = "virtual point runtime value not found" }));
        endpoints.MapGet("/api/point-resolution/{pointKey}", async (string pointKey, string? executionContextId, string? executionInstanceId, IExecutionConfigurationService service, CancellationToken token) =>
            await Map(() => service.ResolvePointAsync(pointKey, executionContextId, executionInstanceId, token)));

        endpoints.MapGet("/api/execution-contexts/{contextId}/deployments", async (string contextId, IExecutionConfigurationService service, CancellationToken token) => await Map(() => service.ListDeploymentsAsync(contextId, token)));
        endpoints.MapPost("/api/execution-contexts/{contextId}/deployments", async (string contextId, HttpRequest request, IExecutionConfigurationService service, IOptions<JsonOptions> options, CancellationToken token) => await DecodeAndMap<ExecutionContextDeployment>(request, options, value => EnsureId(contextId, value.ExecutionContextId, () => service.SaveDeploymentAsync(value, true, token)), 201, token));
        endpoints.MapPut("/api/execution-contexts/{contextId}/deployments/{id}", async (string contextId, string id, HttpRequest request, IExecutionConfigurationService service, IOptions<JsonOptions> options, CancellationToken token) => await DecodeAndMap<ExecutionContextDeployment>(request, options, value => EnsureId(id, value.Id, () => EnsureId(contextId, value.ExecutionContextId, () => service.SaveDeploymentAsync(value, false, token))), 200, token));
        endpoints.MapDelete("/api/execution-contexts/{contextId}/deployments/{id}", async (string contextId, string id, IExecutionConfigurationService service, CancellationToken token) => await MapDelete(() => service.DeleteDeploymentAsync(contextId, id, token)));
        endpoints.MapGet("/api/migrations/virtual-points/report", async (IVirtualPointMigrationService service, CancellationToken token) => Results.Json(await service.RunAsync(false, token)));
        endpoints.MapPost("/api/migrations/virtual-points/apply", async (IVirtualPointMigrationService service, CancellationToken token) => Results.Json(await service.RunAsync(true, token)));
        return endpoints;
    }

    private static async Task<IResult> DecodeAndMap<T>(HttpRequest request, IOptions<JsonOptions> options, Func<T, Task<T>> operation, int status, CancellationToken token)
    {
        try
        {
            var value = await request.ReadFromJsonAsync<T>(options.Value.SerializerOptions, token);
            return value is null ? Results.BadRequest(new { error = "request body must contain JSON" }) : await Map(() => operation(value), status);
        }
        catch (System.Text.Json.JsonException exception) { return Error(400, "invalid_json", exception.Message); }
    }

    private static Task<T> EnsureId<T>(string pathId, string bodyId, Func<Task<T>> operation) =>
        pathId == bodyId ? operation() : throw new ExecutionConfigurationException("resource id must match the request path");

    private static async Task<IResult> Map<T>(Func<Task<T>> operation, int status = 200)
    {
        try { return Results.Json(await operation(), statusCode: status); }
        catch (ExecutionConfigurationException exception) { return Error(exception.StatusCode, exception.Code, exception.Message, exception.Details); }
    }

    private static async Task<IResult> MapDelete(Func<Task> operation)
    {
        try { await operation(); return Results.NoContent(); }
        catch (ExecutionConfigurationException exception) { return Error(exception.StatusCode, exception.Code, exception.Message, exception.Details); }
    }

    private static IResult Error(int status, string code, string message, object? details = null) =>
        Results.Json(new DefinitionErrorResponse(message, code, details), statusCode: status);
}