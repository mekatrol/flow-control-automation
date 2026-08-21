using Server.Common.Contracts;
using Server.Common.Services;

namespace Server.Services.Implementation;

internal sealed class PointReadService(
    IPointDefinitionStore definitions,
    IPointSourceService sources,
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

        if (point.Implementation == "virtual")
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

        var sourceId = point.SourceId;
        if (sourceId is null && point.GroupId is not null)
        {
            sourceId = (await definitions.GetGroupAsync(
                point.GroupId,
                cancellationToken)).SourceId;
        }

        if (sourceId is null)
        {
            return Unavailable(point, "unconfigured", "Point has no direct or inherited source.");
        }

        PointSource source;
        try
        {
            source = await sources.GetAsync(sourceId, cancellationToken);
        }
        catch (PointSourceNotFoundException)
        {
            return Unavailable(point, "source_missing", "Referenced point source is unavailable.");
        }

        if (!source.Enabled)
        {
            return Unavailable(point, "disconnected", "Referenced point source is disabled.");
        }

        // Live adapter activation is deliberately explicit. Until a driver has
        // produced a typed sample, the API reports unavailable rather than
        // presenting a connectivity check or definition default as live data.
        return Unavailable(
            point,
            "disconnected",
            $"{SourceLabel(source.Kind)} read adapter has not produced a live sample.");
    }

    private static PointRuntimeEnvelope Unavailable(
        FlowPoint point,
        string reliability,
        string diagnostic) =>
        new(
            point.Id,
            null,
            point.Units,
            DataQuality.Unavailable,
            reliability,
            null,
            null,
            "disconnected",
            "unavailable",
            diagnostic);

    private static string SourceLabel(string kind) => kind switch
    {
        "homeAssistant" => "Home Assistant",
        "mqtt" => "MQTT",
        "httpJson" => "HTTP/JSON",
        _ => "Point source",
    };
}
