using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Server.Api.Security;

public sealed class ApiAccessMiddleware(RequestDelegate next, IHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context, IOptions<ApiAccessOptions> configured)
    {
        if (!context.Request.Path.StartsWithSegments("/api") || context.Request.Path == "/api/health") { await next(context); return; }
        string actor;
        HashSet<string> permissions;
        if (environment.IsEnvironment("Testing"))
        {
            actor = "test-admin";
            permissions = new HashSet<string>(["*"], StringComparer.Ordinal);
        }
        else
        {
            var supplied = context.Request.Headers["X-Api-Key"].ToString();
            var identity = configured.Value.Identities.FirstOrDefault(item => Matches(item.Value.Key, supplied));
            if (identity.Key is null) { context.Response.StatusCode = 401; await context.Response.WriteAsJsonAsync(new { message = "A valid API key is required.", code = "unauthenticated" }); return; }
            actor = identity.Key;
            permissions = identity.Value.Permissions.ToHashSet(StringComparer.Ordinal);
        }
        var required = RequiredPermission(context.Request);
        if (!permissions.Contains("*") && !permissions.Contains(required))
        {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsJsonAsync(new { message = $"Permission '{required}' is required.", code = "forbidden", details = new { requiredPermission = required } });
            return;
        }
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, actor), new Claim("permission", required)], "ApiKey"));
        await next(context);
    }

    private static string RequiredPermission(HttpRequest request)
    {
        var path = request.Path.Value ?? string.Empty;
        if (path.Contains("/deployments", StringComparison.Ordinal))
        {
            return request.Method == "GET" ? "contexts.view" : "deployments.manage";
        }

        if (path.Contains("/runtime", StringComparison.Ordinal))
        {
            return request.Method == "GET" ? "points.view" : "points.command";
        }

        if (path.Contains("/retained", StringComparison.Ordinal))
        {
            return request.Method == "GET" ? "points.view" : "points.manage-retained";
        }

        if (path.StartsWith("/api/execution-", StringComparison.Ordinal) || path.StartsWith("/api/point-resolution", StringComparison.Ordinal))
        {
            return request.Method == "GET" ? "contexts.view" : "contexts.edit";
        }

        if (path.StartsWith("/api/points", StringComparison.Ordinal) || path.StartsWith("/api/point-groups", StringComparison.Ordinal))
        {
            return request.Method == "GET" ? "points.view" : "points.edit";
        }

        return request.Method == "GET" ? "system.view" : "system.manage";
    }

    private static bool Matches(string expected, string supplied)
    {
        var left = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        var right = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        return CryptographicOperations.FixedTimeEquals(left, right);
    }
}