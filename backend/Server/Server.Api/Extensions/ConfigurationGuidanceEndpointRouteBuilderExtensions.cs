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
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new MemoryStream();

            await request.Body.CopyToAsync(stream, cancellationToken);

            var yaml = stream.ToArray();

            var markdown = ConfigurationGuidance.ConfigurationGuidance.Render(
                configurationType,
                yaml,
                request.Query["pointId"].ToString());

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

}