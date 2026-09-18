using Server.Common.Contracts.Templating;
using System.Globalization;
using System.Text.Json.Nodes;
#pragma warning disable CC0001, IDE0011

namespace Server.Services.Points.Runtime;

internal sealed class HttpPointMappingAdapter(
    IDnsLookup dns,
    ICredentialResolver credentials,
    IHttpProtocolCheck http,
    ITemplateService templates,
    TimeProvider timeProvider,
    IPointValueConverter values) : IPointMappingAdapter
{
    public PointSourceKind Kind => PointSourceKind.Http;

    public async Task<PointMappingReadResult> ReadAsync(PointMappingResolution resolution, CancellationToken cancellationToken) =>
        await ReadMappingAsync(resolution.Source, resolution.Mapping, cancellationToken);

    public async Task<PointMappingReadResult> ReadMappingAsync(
        PointSource source, PointMapping mapping, CancellationToken cancellationToken)
    {
        var operation = mapping.Read
            ?? throw new InvalidOperationException("Mapping does not support reads.");
        var endpoint = BuildEndpoint(source.Connection.BaseUrl!, operation.Path!);
        var addresses = await ResolveAddresses(source, endpoint, cancellationToken);
        var credential = await credentials.ResolveAsync(source.CredentialRef ?? string.Empty, cancellationToken);
        var response = await http.ReadAsync(
            source, endpoint, operation.Accept, credential, addresses, cancellationToken);
        if (response.Diagnostic is not null || response.Response is null)
            return Failed(response.Diagnostic ?? "HTTP response was unavailable.", response.Response);

        try
        {
            var model = ResponseModel(operation.PayloadFormat!, response.Response.Body);
            var rendered = templates.Render(operation.Template!, ToTemplateObject(model), RenderAs.Json);
            var aliases = JsonNode.Parse(rendered) as JsonObject
                ?? throw new InvalidOperationException("Read template must render a JSON object.");
            if (aliases.Count != mapping.Aliases.Count
                || aliases.Any(item => !mapping.Aliases.Contains(item.Key, StringComparer.Ordinal)))
                throw new InvalidOperationException("Read template aliases do not match the mapping declaration.");

            return new(aliases.ToDictionary(item => item.Key, item => Raw(item.Value), StringComparer.Ordinal),
                DataQualityType.Good, timeProvider.GetUtcNow(), Response: response.Response);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Failed($"HTTP mapping output was invalid: {exception.Message}", response.Response);
        }
    }

    public async Task<PointMappingCommandResult> CommandAsync(PointMappingResolution resolution, object? value, CancellationToken cancellationToken)
    {
        var operation = resolution.Mapping.Command
            ?? throw new InvalidOperationException("Mapping does not support commands.");
        var raw = values.Serialize(resolution.Point, value);
        var rendered = templates.Render(operation.Template!,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [resolution.Alias] = raw },
            operation.PayloadFormat == "json" ? RenderAs.Json : RenderAs.Text);
        var endpoint = BuildEndpoint(resolution.Source.Connection.BaseUrl!, operation.Path!);
        var addresses = await ResolveAddresses(resolution.Source, endpoint, cancellationToken);
        var credential = await credentials.ResolveAsync(resolution.Source.CredentialRef ?? string.Empty, cancellationToken);
        var response = await http.WriteAsync(resolution.Source, endpoint, operation.Method!, rendered,
            ContentType(operation), credential, addresses, cancellationToken);
        return new(rendered, timeProvider.GetUtcNow(), response.Diagnostic, response.Response);
    }

    public async Task<PointMappingCommandResult> CommandMappingAsync(
        PointSource source, PointMapping mapping, string payload, CancellationToken cancellationToken)
    {
        var operation = mapping.Command
            ?? throw new InvalidOperationException("Mapping does not support commands.");
        var endpoint = BuildEndpoint(source.Connection.BaseUrl!, operation.Path!);
        var addresses = await ResolveAddresses(source, endpoint, cancellationToken);
        var credential = await credentials.ResolveAsync(source.CredentialRef ?? string.Empty, cancellationToken);
        var response = await http.WriteAsync(source, endpoint, operation.Method!, payload,
            ContentType(operation), credential, addresses, cancellationToken);

        return new(payload, timeProvider.GetUtcNow(), response.Diagnostic, response.Response);
    }

    private async Task<IReadOnlyList<System.Net.IPAddress>> ResolveAddresses(PointSource source, Uri endpoint, CancellationToken token)
    {
        var addresses = await dns.LookupAsync(endpoint.Host, token);
        if (addresses.Count == 0 || addresses.Any(address => Server.Services.Communication.Network.ConnectivityPolicy.IsForbidden(address, source.Connection.AllowPrivateNetwork == true)))
            throw new InvalidOperationException("HTTP destination is forbidden or unavailable.");
        return addresses;
    }

    internal static Uri BuildEndpoint(string baseUrl, string mappingPath) =>
        new(baseUrl.TrimEnd('/') + "/" + mappingPath.TrimStart('/'), UriKind.Absolute);

    private static JsonNode ResponseModel(string format, string body) => format switch
    {
        "json" => JsonNode.Parse(body)
            ?? throw new InvalidOperationException("Response JSON was null."),
        "text" => new JsonObject { ["response"] = body },
        _ => throw new InvalidOperationException($"Unsupported response format '{format}'.")
    };

    private static string ContentType(MappingCommandOperation operation) => operation.PayloadFormat switch
    {
        "json" => operation.ContentType ?? "application/json",
        "text" => operation.ContentType ?? "text/plain",
        _ => throw new InvalidOperationException($"Unsupported command body format '{operation.PayloadFormat}'.")
    };

    private PointMappingReadResult Failed(string diagnostic, HttpResponsePreview? response) =>
        new(new Dictionary<string, string?>(), DataQualityType.Unavailable, timeProvider.GetUtcNow(), diagnostic, response);

    private static object ToTemplateObject(JsonNode node) => node switch
    {
        JsonObject value => value.ToDictionary(item => item.Key, item => item.Value is null ? null : ToTemplateObject(item.Value), StringComparer.Ordinal),
        JsonArray value => value.Select(item => item is null ? null : ToTemplateObject(item)).ToArray(),
        JsonValue value when value.TryGetValue<bool>(out var boolean) => boolean,
        JsonValue value when value.TryGetValue<long>(out var integer) => integer,
        JsonValue value when value.TryGetValue<double>(out var number) => number,
        JsonValue value when value.TryGetValue<string>(out var text) => text,
        _ => node.ToJsonString()
    };

    private static string? Raw(JsonNode? node) => node switch
    {
        null => null,
        JsonValue value when value.TryGetValue<string>(out var text) => text,
        JsonValue value when value.TryGetValue<bool>(out var boolean) => boolean ? "true" : "false",
        JsonValue value when value.TryGetValue<long>(out var integer) => integer.ToString(CultureInfo.InvariantCulture),
        JsonValue value when value.TryGetValue<double>(out var number) => number.ToString("R", CultureInfo.InvariantCulture),
        _ => throw new InvalidOperationException("Every mapping alias must be a scalar or null.")
    };
}
