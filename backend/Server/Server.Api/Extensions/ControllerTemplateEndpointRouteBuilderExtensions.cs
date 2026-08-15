using Server.Api.Contracts;
using Server.Services;
using Server.Services.Contracts;
using System.Globalization;
using System.Text;

namespace Server.Api.Extensions;

public static class ControllerTemplateEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapControllerTemplateEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/controller-templates", List);
        endpoints.MapPost("/api/controller-templates/validate", Validate);
        endpoints.MapPost("/api/controller-templates", Create);
        endpoints.MapGet("/api/controller-templates/{templateId}", Get);
        endpoints.MapPut("/api/controller-templates/{templateId}", Update);
        endpoints.MapDelete("/api/controller-templates/{templateId}", Delete);
        endpoints.MapGet("/api/controller-templates/{templateId}/yaml", GetYaml);
        return endpoints;
    }

    private static async Task<IResult> List(
        IControllerTemplateStore templates,
        CancellationToken cancellationToken) =>
        Results.Json(new { items = await templates.ListAsync(cancellationToken) });

    private static async Task<IResult> Get(
        string templateId,
        HttpResponse response,
        IControllerTemplateStore templates,
        CancellationToken cancellationToken) =>
        await Read(
            response,
            () => templates.GetAsync(templateId, cancellationToken),
            yaml: false);

    private static async Task<IResult> GetYaml(
        string templateId,
        HttpResponse response,
        IControllerTemplateStore templates,
        CancellationToken cancellationToken) =>
        await Read(
            response,
            () => templates.GetAsync(templateId, cancellationToken),
            yaml: true);

    private static async Task<IResult> Validate(
        HttpRequest request,
        IControllerTemplateValidator validator,
        CancellationToken cancellationToken)
    {
        var decoded = await Decode(request, cancellationToken);
        if (decoded.Error is not null)
        {
            return decoded.Error;
        }

        try
        {
            validator.Validate(decoded.Value!);
            return Results.Json(new { valid = true, diagnostics = Array.Empty<object>() });
        }
        catch (ControllerTemplateValidationException exception)
        {
            return Results.Json(new { valid = false, diagnostics = exception.Diagnostics });
        }
    }

    private static async Task<IResult> Create(
        HttpRequest request,
        HttpResponse response,
        IControllerTemplateStore templates,
        CancellationToken cancellationToken)
    {
        var decoded = await Decode(request, cancellationToken);
        return decoded.Error
            ?? await Write(
                response,
                () => templates.CreateAsync(decoded.Value!, cancellationToken),
                StatusCodes.Status201Created);
    }

    private static async Task<IResult> Update(
        string templateId,
        HttpRequest request,
        HttpResponse response,
        IControllerTemplateStore templates,
        CancellationToken cancellationToken)
    {
        var decoded = await Decode(request, cancellationToken);
        if (decoded.Error is not null)
        {
            return decoded.Error;
        }

        return TryRevision(request.Headers.IfMatch.ToString(), out var revision)
            ? await Write(
                response,
                () => templates.UpdateAsync(
                    templateId,
                    decoded.Value!,
                    revision,
                    cancellationToken),
                StatusCodes.Status200OK)
            : Error(400, "invalid_revision", "If-Match must be a positive integer");
    }

    private static async Task<IResult> Delete(
        string templateId,
        HttpRequest request,
        IControllerTemplateStore templates,
        CancellationToken cancellationToken)
    {
        if (!TryRevision(request.Query["revision"].ToString(), out var revision))
        {
            return Error(400, "invalid_revision", "revision must be a positive integer");
        }

        try
        {
            await templates.DeleteAsync(templateId, revision, cancellationToken);
            return Results.NoContent();
        }
        catch (ControllerTemplateNotFoundException exception)
        {
            return Error(404, "not_found", exception.Message);
        }
        catch (ControllerTemplateConflictException exception)
        {
            return Error(409, ConflictCode(exception), exception.Message);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Error(500, "persistence_failed", "unable to persist controller template");
        }
    }

    private static async Task<IResult> Read(
        HttpResponse response,
        Func<Task<ControllerTemplate>> operation,
        bool yaml)
    {
        try
        {
            var template = await operation();
            response.Headers.ETag = template.Revision.ToString(CultureInfo.InvariantCulture);
            return yaml
                ? Results.Text(
                    ControllerTemplateYaml.Render(template),
                    "application/yaml",
                    Encoding.UTF8)
                : Results.Json(template);
        }
        catch (ControllerTemplateNotFoundException exception)
        {
            return Error(404, "not_found", exception.Message);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Error(500, "persistence_failed", "unable to read controller template");
        }
    }

    private static async Task<IResult> Write(
        HttpResponse response,
        Func<Task<ControllerTemplate>> operation,
        int status)
    {
        try
        {
            var template = await operation();
            response.Headers.ETag = template.Revision.ToString(CultureInfo.InvariantCulture);
            return Results.Json(template, statusCode: status);
        }
        catch (ControllerTemplateNotFoundException exception)
        {
            return Error(404, "not_found", exception.Message);
        }
        catch (ControllerTemplateConflictException exception)
        {
            return Error(409, ConflictCode(exception), exception.Message);
        }
        catch (ControllerTemplateValidationException exception)
        {
            return Error(400, "validation_failed", exception.Message, new
            {
                diagnostics = exception.Diagnostics
            });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Error(500, "persistence_failed", "unable to persist controller template");
        }
    }

    private static async Task<(ControllerTemplate? Value, IResult? Error)> Decode(
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
                return (null, Error(
                    400,
                    "request_too_large",
                    "controller template YAML exceeds 256 KiB"));
            }

            return (ControllerTemplateYaml.Parse(buffer.AsSpan(0, length)), null);
        }
        catch (ConfigurationYamlException exception)
        {
            var details = exception.InnerException is not YamlException yaml ? null : new
            {
                line = yaml.Start.Line,
                column = yaml.Start.Column
            };
            return (null, Error(
                400,
                YamlCode(exception.Category),
                exception.Message,
                details));
        }
    }

    private static bool TryRevision(string value, out int revision) =>
        int.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out revision)
        && revision > 0;

    private static string ConflictCode(ControllerTemplateConflictException exception) =>
        exception.Message.Contains("stale revision", StringComparison.Ordinal)
            ? "stale_revision"
            : exception.Message.Contains("read-only", StringComparison.Ordinal)
                ? "read_only"
                : "resource_conflict";

    private static string YamlCode(ConfigurationYamlError error) => error switch
    {
        ConfigurationYamlError.Syntax => "yaml_syntax",
        ConfigurationYamlError.TooLarge => "request_too_large",
        ConfigurationYamlError.MultipleDocuments => "multiple_yaml_documents",
        ConfigurationYamlError.UnsupportedFeature => "unsupported_yaml",
        ConfigurationYamlError.UnsupportedSchema => "unsupported_schema",
        _ => "invalid_yaml",
    };

    private static IResult Error(
        int status,
        string code,
        string message,
        object? details = null) =>
        Results.Json(
            new DefinitionErrorResponse(message, code, details),
            statusCode: status);
}