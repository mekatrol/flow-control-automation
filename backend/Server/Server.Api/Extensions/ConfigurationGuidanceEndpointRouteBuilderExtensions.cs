using Server.Services;
using System.Text.Json.Nodes;

namespace Server.Api.Extensions;

public static class ConfigurationGuidanceEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapConfigurationGuidanceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/configuration-guidance/{configurationType}", Render);

        return endpoints;
    }

    private static async Task<IResult> Render(
        string configurationType,
        HttpRequest request,
        IPointSourceService sources,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new MemoryStream();
            await request.Body.CopyToAsync(stream, cancellationToken);
            var yaml = stream.ToArray();
            var sourceKind = await ResolvePointSourceKind(
                configurationType,
                yaml,
                sources,
                cancellationToken);
            var markdown = ConfigurationGuidance.ConfigurationGuidance.Render(
                configurationType,
                yaml,
                sourceKind);

            return Results.Text(markdown, "text/markdown; charset=utf-8");
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
        catch (ConfigurationYamlException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }

    private static async Task<string?> ResolvePointSourceKind(
        string configurationType,
        byte[] yaml,
        IPointSourceService sources,
        CancellationToken cancellationToken)
    {
        if (configurationType != "point")
        {
            return null;
        }

        var document = ConfigurationYaml.ParseToJson(yaml, ConfigurationKind.Points);
        var point = document["points"]?[0];
        var sourceType = StringValue(point?["pointSourceType"]);
        var sourceId = StringValue(point?["sourceId"]);

        if (sourceType != "remote" || string.IsNullOrWhiteSpace(sourceId))
        {
            return null;
        }

        try
        {
            return (await sources.GetAsync(sourceId, cancellationToken)).Kind;
        }
        catch (PointSourceNotFoundException)
        {
            return null;
        }
    }

    private static string? StringValue(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var result) ? result : null;
}