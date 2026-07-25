namespace Server.Api.Extensions;

public static class EndpointRouteBuilderExtensions
{
    public const string HealthRoute = "/api/health";

    public static IEndpointRouteBuilder MapFlowControlEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(HealthRoute, static () => Results.Json(new { status = "ok" }));
        return endpoints;
    }
}