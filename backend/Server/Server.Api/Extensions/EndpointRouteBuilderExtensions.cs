using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Server.Api.Contracts;
using Server.Common.Models;
using Server.Compiler;
using Server.Compiler.Contracts;
using Server.Compiler.Services;
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
        endpoints.MapFlowDebugEndpoints();
        endpoints.MapFlowEmulatorEndpoints();
        endpoints.MapFlowSimulatorEndpoints();
        endpoints.MapGet("/api/flows", ListFlows);
        endpoints.MapPost("/api/flows", CreateFlow);
        endpoints.MapPost("/api/flows/import-il", ImportFlowIl);
        endpoints.MapGet("/api/flows/{flowId}", GetFlow);
        endpoints.MapPut("/api/flows/{flowId}", SaveFlow);
        endpoints.MapPost("/api/flows/{flowId}/compile", CompileFlow);
        endpoints.MapGet("/api/flows/{flowId}/deployed", GetDeployedFlow);
        endpoints.MapPost("/api/flows/{flowId}/revert-to-deployed", RevertToDeployedFlow);
        endpoints.MapDelete("/api/flows/{flowId}", DeleteFlow);
        endpoints.MapPost("/api/flows/{flowId}/deploy", DeployFlow);
        endpoints.MapPost("/api/flows/{flowId}/disable", DisableFlow);
        endpoints.MapPost("/api/flows/{flowId}/enable", EnableFlow);
        endpoints.MapGet("/api/flows/{flowId}/runtime", GetRuntime);
        endpoints.MapPost("/api/flows/{flowId}/runtime/scan", ScanFlowOnce);
        endpoints.MapPointSourceEndpoints();
        endpoints.MapPointDefinitionEndpoints();
        endpoints.MapControllerTemplateEndpoints();
        endpoints.MapCredentialEndpoints();
        endpoints.MapExecutionConfigurationEndpoints();
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

    private static async Task<IResult> ImportFlowIl(
        HttpRequest request,
        IFlowDecompiler decompiler,
        IFlowService flows,
        IOptions<JsonOptions> jsonOptions,
        CancellationToken cancellationToken)
    {
        var decoded = await DecodeAsync<ImportFlowIlRequest>(
            request,
            jsonOptions.Value.SerializerOptions,
            cancellationToken);

        if (decoded.Error is not null)
        {
            return decoded.Error;
        }

        byte[] artifact;
        try
        {
            artifact = Convert.FromBase64String(decoded.Value!.ArtifactBase64);
        }
        catch (FormatException)
        {
            return Error(StatusCodes.Status400BadRequest, "artifactBase64 must be valid Base64");
        }

        try
        {
            var recovered = decompiler.Decompile(artifact, decoded.Value.Name);
            var flow = recovered.Flow;
            if (decoded.Value.Save)
            {
                var created = await flows.CreateAsync(flow.Name, cancellationToken);
                flow = await flows.SaveAsync(created.Id, flow with { Id = created.Id }, cancellationToken);
            }

            return Results.Json(new ImportFlowIlResponse(
                flow,
                recovered.RecoveryLevel,
                recovered.Warnings,
                recovered.Provenance,
                decoded.Value.Save),
                statusCode: decoded.Value.Save ? StatusCodes.Status201Created : StatusCodes.Status200OK);
        }
        catch (FlowDecompilationException exception)
        {
            return Results.Json(exception.Diagnostic, statusCode: StatusCodes.Status422UnprocessableEntity);
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
            return Error(StatusCodes.Status500InternalServerError, "unable to import Flow IL");
        }
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

    private static async Task<IResult> CompileFlow(
        string flowId,
        ExecutableFlowSource source,
        IFlowCompilationTargetResolver targetResolver,
        IFlowCompiler compiler,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(flowId, source.Id, StringComparison.Ordinal))
        {
            return Error(StatusCodes.Status400BadRequest, "flow id must match the request path");
        }

        try
        {
            var target = await targetResolver.ResolveAsync(source, cancellationToken);
            var result = compiler.Compile(new FlowCompilationRequest { Source = source, Target = target });
            return Results.Json(new
            {
                success = true,
                result.FlowRevision,
                result.ArtifactSha256,
                result.InstructionCount,
                result.SlotCount,
                result.PointCount,
                diagnostics = Array.Empty<FlowCompilationDiagnostic>()
            });
        }
        catch (FlowCompilationException exception)
        {
            return Results.Json(new
            {
                success = false,
                diagnostics = exception.Diagnostics
            }, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
        catch (ControllerGatewayException exception)
        {
            return Results.Json(new
            {
                success = false,
                diagnostics = new[]
                {
                    new
                    {
                        code = "TargetInvalid",
                        displayCode = "FLOW-TARGET",
                        path = "/controllerTemplateId",
                        title = "Compilation target is unavailable",
                        message = exception.Message
                    }
                }
            }, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }

    private static async Task<IResult> GetDeployedFlow(
        string flowId,
        IFlowService flows,
        CancellationToken cancellationToken)
    {
        try
        {
            var flow = await flows.GetAsync(flowId, cancellationToken);
            var version = flow.DeployedVersion
                ?? throw new FlowValidationException("flow has no deployed version");
            return Results.Json(flow with
            {
                Name = version.Name,
                Description = version.Description,
                UpdatedAt = version.UpdatedAt,
                Revision = version.Revision,
                Nodes = version.Nodes,
                Connections = version.Connections,
                Status = "deployed"
            });
        }
        catch (FlowNotFoundException)
        {
            return Error(StatusCodes.Status404NotFound, "flow not found");
        }
        catch (FlowValidationException exception)
        {
            return Error(StatusCodes.Status409Conflict, exception.Message);
        }
    }

    private static Task<IResult> RevertToDeployedFlow(
        string flowId,
        IFlowService flows,
        CancellationToken cancellationToken) =>
        MapFlowResult(() => flows.RevertToDeployedAsync(flowId, cancellationToken));

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
        IFlowDeploymentService deployment,
        CancellationToken cancellationToken)
    {
        try
        {
            var flow = await flows.GetAsync(flowId, cancellationToken);
            var snapshot = await deployment.DeployAsync(flow, cancellationToken);
            await flows.MarkDeployedAsync(flowId, flow.Revision, cancellationToken);
            return Results.Json(snapshot);
        }
        catch (FlowNotFoundException)
        {
            return Error(StatusCodes.Status404NotFound, "flow not found");
        }
        catch (FlowCompilationException exception)
        {
            return Results.Json(exception.Diagnostics, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
        catch (FlowVmException exception)
        {
            return Results.Json(
                FlowCompilationDiagnostics.Create(
                    FlowCompilationDiagnosticCode.VmPrepareFailed,
                    exception.Path),
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Error(StatusCodes.Status503ServiceUnavailable, "runtime service unavailable");
        }
    }

    private static async Task<IResult> ScanFlowOnce(
        string flowId,
        IFlowService flows,
        IFlowRuntimeService runtime,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Json(await runtime.ScanOnceAsync(
                await flows.GetAsync(flowId, cancellationToken),
                cancellationToken));
        }
        catch (FlowNotFoundException)
        {
            return Error(StatusCodes.Status404NotFound, "flow not found");
        }
        catch (InvalidOperationException)
        {
            return Error(StatusCodes.Status409Conflict, "flow is not deployed");
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