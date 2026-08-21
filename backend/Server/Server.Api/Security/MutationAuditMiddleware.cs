using Server.Services;

namespace Server.Api.Security;

public sealed class MutationAuditMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IAuditService audit)
    {
        if (context.Request.Method is "GET" or "HEAD" or "OPTIONS")
        {
            await next(context);
            return;
        }

        var actor = context.User.Identity?.Name
            ?? throw new InvalidOperationException("Authenticated mutation has no actor.");
        await next(context);
        await audit.RecordAsync(
            actor,
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode,
            CancellationToken.None);
    }
}