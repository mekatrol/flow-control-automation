using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Server.Api.Contracts;
using Server.Services;
using Server.Services.Contracts;
using System.Globalization;
using System.Text.Json;

namespace Server.Api.Extensions;

public static class CredentialEndpointRouteBuilderExtensions
{
    private const long MaximumRequestBytes = 64L << 10;

    public static IEndpointRouteBuilder MapCredentialEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/credentials", List);
        endpoints.MapPost("/api/credentials", Create);
        endpoints.MapGet("/api/credentials/{credentialId}", Get);
        endpoints.MapPut("/api/credentials/{credentialId}", Update);
        endpoints.MapDelete("/api/credentials/{credentialId}", Delete);
        return endpoints;
    }

    private static async Task<IResult> List(
        ICredentialStore credentials,
        CancellationToken cancellationToken) =>
        Results.Json(
            new CredentialListResponse(
                await credentials.ListAsync(cancellationToken)));

    private static async Task<IResult> Create(
        HttpRequest request,
        ICredentialStore credentials,
        IOptions<JsonOptions> jsonOptions,
        CancellationToken cancellationToken)
    {
        var decoded = await Decode(
            request,
            jsonOptions.Value.SerializerOptions,
            cancellationToken);
        return decoded.Error
            ?? await WriteResult(
                () => credentials.CreateAsync(decoded.Input!, cancellationToken),
                StatusCodes.Status201Created);
    }

    private static async Task<IResult> Get(
        string credentialId,
        ICredentialStore credentials,
        CancellationToken cancellationToken) =>
        await WriteResult(
            () => credentials.GetAsync(credentialId, cancellationToken),
            StatusCodes.Status200OK);

    private static async Task<IResult> Update(
        string credentialId,
        HttpRequest request,
        ICredentialStore credentials,
        IOptions<JsonOptions> jsonOptions,
        CancellationToken cancellationToken)
    {
        var decoded = await Decode(
            request,
            jsonOptions.Value.SerializerOptions,
            cancellationToken);
        return decoded.Error
            ?? await WriteResult(
                () => credentials.UpdateAsync(
                    credentialId,
                    decoded.Input!,
                    cancellationToken),
                StatusCodes.Status200OK);
    }

    private static async Task<IResult> Delete(
        string credentialId,
        HttpRequest request,
        ICredentialStore credentials,
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
            await credentials.DeleteAsync(credentialId, revision, cancellationToken);
            return Results.NoContent();
        }
        catch (CredentialNotFoundException)
        {
            return Error(StatusCodes.Status404NotFound, "credential not found");
        }
        catch (CredentialConflictException exception)
        {
            return Error(StatusCodes.Status409Conflict, exception.Message);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Error(
                StatusCodes.Status500InternalServerError,
                "unable to persist credential");
        }
    }

    private static async Task<IResult> WriteResult(
        Func<Task<CredentialMetadata>> operation,
        int status)
    {
        try
        {
            return Results.Json(await operation(), statusCode: status);
        }
        catch (CredentialNotFoundException)
        {
            return Error(StatusCodes.Status404NotFound, "credential not found");
        }
        catch (CredentialConflictException exception)
        {
            return Error(StatusCodes.Status409Conflict, exception.Message);
        }
        catch (CredentialValidationException exception)
        {
            return Error(StatusCodes.Status400BadRequest, exception.Message);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Error(
                StatusCodes.Status500InternalServerError,
                "unable to persist credential");
        }
    }

    private static async Task<(CredentialInput? Input, IResult? Error)> Decode(
        HttpRequest request,
        JsonSerializerOptions options,
        CancellationToken cancellationToken)
    {
        if (request.ContentLength > MaximumRequestBytes)
        {
            return (
                null,
                Error(StatusCodes.Status400BadRequest, "http: request body too large"));
        }

        try
        {
            request.HttpContext.Features
                .Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>()
                ?.MaxRequestBodySize = MaximumRequestBytes;
            var input = await JsonSerializer.DeserializeAsync<CredentialInput>(
                request.Body,
                options,
                cancellationToken);
            return input is null
                ? (null, Error(StatusCodes.Status400BadRequest, "request body must contain JSON"))
                : (input, null);
        }
        catch (JsonException exception)
        {
            return (null, Error(StatusCodes.Status400BadRequest, exception.Message));
        }
        catch (BadHttpRequestException exception)
        {
            return (null, Error(StatusCodes.Status400BadRequest, exception.Message));
        }
    }

    private static IResult Error(int status, string message) =>
        Results.Json(new ErrorResponse(message), statusCode: status);
}