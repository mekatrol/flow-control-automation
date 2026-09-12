using System.Text.Json.Nodes;

namespace Server.Services.Points.Runtime;

internal sealed class PointReadService(
    IPointDefinitionStore definitions,
    IPointSourceService sources,
    IDnsLookup dns,
    ICredentialResolver credentials,
    IHttpProtocolCheck http,
    IVirtualPointRuntimeStore? virtualPoints = null) : IPointReadService
{
    public async Task<PointRuntimeEnvelope> ReadAsync(
        string pointId,
        CancellationToken cancellationToken)
    {
        var point = await definitions.GetPointAsync(pointId, cancellationToken);

        if (!point.Enabled)
        {
            return Unavailable(point, "disabled", "Point is disabled.");
        }

        if (!point.Readable)
        {
            return Unavailable(point, "not_readable", "Point is not configured for reads.");
        }

        var sourcePage = await sources.ListAsync(new PointSourceListOptions(), cancellationToken);
        var source = sourcePage.Items.SingleOrDefault(candidate =>
            candidate.Points.Any(nested => nested.Id == point.Id));

        if (source?.Kind == "virtual" || point.Direction == DataDirectionType.Value)
        {
            if (virtualPoints is not null && virtualPoints.TrySnapshot("server", point.Id, out var snapshot))
            {
                var value = snapshot.Value;

                return new PointRuntimeEnvelope(
                    point.Id,
                    value is null ? null : value.DataType == DataType.Number
                        ? System.Text.Json.Nodes.JsonValue.Create(value.Number)
                        : System.Text.Json.Nodes.JsonValue.Create(value.Boolean),
                    point.Units,
                    snapshot.Quality,
                    value is null ? "not_initialized" : "reliable",
                    snapshot.Timestamp,
                    snapshot.Timestamp,
                    "connected",
                    value is null ? "unavailable" : "ok",
                    value is null ? "Virtual point has no commissioned runtime value." : string.Empty);
            }

            return Unavailable(
                point,
                "not_initialized",
                "Virtual point has no commissioned runtime value.");
        }

        if (source?.Kind == "physical")
        {
            return Unavailable(point, "unconfigured", "Physical point has no commissioned hardware read adapter.");
        }

        if (source is null)
        {
            return Unavailable(point, "unconfigured", "Point has no source.");
        }

        if (!source.Enabled)
        {
            return Unavailable(point, "disconnected", "Referenced point source is disabled.");
        }

        if (source.Kind == "httpJson")
        {
            return await ReadHttpJson(point, source, cancellationToken);
        }

        return Unavailable(
            point,
            "disconnected",
            $"{SourceLabel(source.Kind)} read adapter has not produced a live sample.");
    }

    private async Task<PointRuntimeEnvelope> ReadHttpJson(
        AutomationPoint point,
        PointSource source,
        CancellationToken cancellationToken)
    {
        var segments = point.Mapping.Split('/', StringSplitOptions.None);
        var mapping = segments.Length == 2
            ? source.Mappings.SingleOrDefault(candidate => candidate.Id == segments[0])
            : null;
        var path = mapping?.Read?.Path;
        string? pointer = null;

        if (string.IsNullOrWhiteSpace(path))
        {
            return Unavailable(point, "unconfigured", "HTTP/JSON point mapping has no path.");
        }

        var endpoint = new Uri(new Uri(source.Connection.BaseUrl!), path);
        IReadOnlyList<System.Net.IPAddress> addresses;

        try
        {
            addresses = await dns.LookupAsync(endpoint.Host, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Unavailable(point, "disconnected", "HTTP/JSON host lookup failed.");
        }

        if (addresses.Count == 0 || addresses.Any(address => Server.Services.Communication.Network.ConnectivityPolicy.IsForbidden(
            address,
            source.Connection.AllowPrivateNetwork == true)))
        {
            return Unavailable(point, "disconnected", "HTTP/JSON destination is forbidden or unavailable.");
        }

        string credential;

        try
        {
            credential = await credentials.ResolveAsync(source.CredentialRef ?? string.Empty, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Unavailable(point, "disconnected", "HTTP/JSON credential could not be resolved.");
        }

        var result = await http.ReadAsync(source, endpoint, credential, addresses, cancellationToken);

        if (result.Diagnostic is not null || result.Response is null)
        {
            return Unavailable(point, "disconnected", result.Diagnostic ?? "HTTP/JSON response was unavailable.");
        }

        try
        {
            var value = JsonNode.Parse(result.Response.Body);

            if (!string.IsNullOrEmpty(pointer))
            {
                foreach (var rawSegment in pointer.Split('/', StringSplitOptions.RemoveEmptyEntries))
                {
                    var segment = rawSegment.Replace("~1", "/").Replace("~0", "~");
                    value = value is JsonArray array && int.TryParse(segment, out var index)
                        ? array[index]
                        : value?[segment];
                }
            }

            if (value is null)
            {
                return Unavailable(
                    point,
                    "bad_data",
                    $"JSON pointer '{pointer}' did not select a value.",
                    result.Response);
            }

            var now = DateTimeOffset.UtcNow.ToString("O");

            return new(
                point.Id,
                value.DeepClone(),
                point.Units,
                DataQualityType.Good,
                "reliable",
                null,
                now,
                "connected",
                "live",
                string.Empty,
                result.Response);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Unavailable(
                point,
                "bad_data",
                "HTTP/JSON response was not valid JSON for this mapping.",
                result.Response);
        }
    }

    private static PointRuntimeEnvelope Unavailable(
        AutomationPoint point,
        string reliability,
        string diagnostic,
        HttpResponsePreview? deviceResponse = null) =>
        new(
            point.Id,
            null,
            point.Units,
            DataQualityType.Unavailable,
            reliability,
            null,
            null,
            deviceResponse is null ? "disconnected" : "connected",
            "unavailable",
            diagnostic,
            deviceResponse);

    private static string SourceLabel(string kind) => kind switch
    {
        "homeAssistant" => "Home Assistant",
        "mqtt" => "MQTT",
        "httpJson" => "HTTP/JSON",
        _ => "Point source"
    };
}