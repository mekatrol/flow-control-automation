using Microsoft.Extensions.Options;
using System.Net;
using System.Text;

namespace Server.Api.Security;

public sealed class FrontendApiKeyInjectionMiddleware(RequestDelegate next)
{
    private const string ApiKeyPlaceholder = "__FLOW_CONTROL_API_KEY__";

    public async Task InvokeAsync(HttpContext context, IOptions<ApiAccessOptions> configured)
    {
        if (!CouldReturnFrontendDocument(context.Request))
        {
            await next(context);
            return;
        }

        var originalBody = context.Response.Body;
        await using var bufferedBody = new MemoryStream();
        context.Response.Body = bufferedBody;

        try
        {
            await next(context);
            bufferedBody.Position = 0;

            if (IsHtmlResponse(context.Response))
            {
                using var reader = new StreamReader(bufferedBody, Encoding.UTF8, leaveOpen: true);
                var html = await reader.ReadToEndAsync(context.RequestAborted);
                var injectedHtml = html.Replace(
                    ApiKeyPlaceholder,
                    WebUtility.HtmlEncode(GetFrontendApiKey(configured.Value)),
                    StringComparison.Ordinal);
                var bytes = Encoding.UTF8.GetBytes(injectedHtml);
                context.Response.ContentLength = bytes.Length;
                context.Response.Headers.CacheControl = "no-store";
                context.Response.Headers.Remove("ETag");
                context.Response.Headers.Remove("Last-Modified");
                await originalBody.WriteAsync(bytes, context.RequestAborted);
            }
            else
            {
                await bufferedBody.CopyToAsync(originalBody, context.RequestAborted);
            }
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }

    private static bool CouldReturnFrontendDocument(HttpRequest request) =>
        HttpMethods.IsGet(request.Method)
        && !request.Path.StartsWithSegments("/api")
        && (!Path.HasExtension(request.Path.Value) || request.Path.Value?.EndsWith("/index.html", StringComparison.OrdinalIgnoreCase) == true);

    private static bool IsHtmlResponse(HttpResponse response) =>
        response.StatusCode is >= 200 and < 300
        && response.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) == true;

    private static string GetFrontendApiKey(ApiAccessOptions options)
    {
        if (options.FrontendIdentity is not null)
        {
            return options.Identities[options.FrontendIdentity].Key;
        }

        return options.Identities.Values.First().Key;
    }
}