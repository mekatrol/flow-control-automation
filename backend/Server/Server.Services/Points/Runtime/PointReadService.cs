namespace Server.Services.Points.Runtime;

#pragma warning disable IDE0011

internal sealed class PointReadService(
    IPointDefinitionStore definitions,
    IPointSourceService sources,
    IPointMappingResolver resolver,
    IPointMappingExecutionService mappings,
    IPointValueConverter values,
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

        if (source?.Kind == PointSourceKind.Virtual || point.Direction == DataDirectionType.Value)
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

        if (source is null)
        {
            return Unavailable(point, "unconfigured", "Point has no source.");
        }

        if (!source.Enabled)
        {
            return Unavailable(point, "disconnected", "Referenced point source is disabled.");
        }

        return await ReadMapping(point, source, cancellationToken);
    }

    private async Task<PointRuntimeEnvelope> ReadMapping(
        AutomationPoint point,
        PointSource source,
        CancellationToken cancellationToken)
    {
        try
        {
            var resolution = resolver.Resolve(source, point);
            var result = await mappings.ReadAsync(resolution, cancellationToken);
            if (result.Quality != DataQualityType.Good)
                return Unavailable(point, "disconnected", result.Diagnostic ?? "Mapping read failed.", result.Response);
            if (!result.Values.TryGetValue(resolution.Alias, out var raw))
                return Unavailable(point, "bad_data", $"Mapping did not return alias '{resolution.Alias}'.", result.Response);
            var converted = values.Parse(point, raw);
            if (converted.Quality != DataQualityType.Good)
                return new(point.Id, null, point.Units, converted.Quality, "bad_data", result.Timestamp.ToString("O"),
                    result.Timestamp.ToString("O"), "connected", "unavailable", converted.Diagnostic ?? "Value conversion failed.", result.Response);

            return new(point.Id, converted.Value, point.Units, DataQualityType.Good, "reliable",
                result.Timestamp.ToString("O"), result.Timestamp.ToString("O"), "connected", "live", string.Empty, result.Response);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Unavailable(point, "disconnected", exception.Message);
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

}