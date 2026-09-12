using Server.Common.Contracts.Points;

namespace Server.Common.Models.Points;

public sealed class PointMappingResolver : IPointMappingResolver
{
    public PointMappingResolution Resolve(PointSource source, string pointId)
    {
        ArgumentNullException.ThrowIfNull(source);
        var point = source.Points.SingleOrDefault(candidate => candidate.Id == pointId)
            ?? throw new ArgumentException($"point '{pointId}' does not belong to source '{source.Id}'", nameof(pointId));

        return Resolve(source, point);
    }

    public PointMappingResolution Resolve(PointSource source, AutomationPoint point)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(point);
        var segments = point.Mapping.Split('/', StringSplitOptions.None);

        if (segments.Length != 2 || segments.Any(string.IsNullOrEmpty))
        {
            throw new ArgumentException($"point '{point.Id}' mapping must be exactly <mapping-id>/<alias>", nameof(point));
        }

        var mapping = source.Mappings.SingleOrDefault(candidate => candidate.Id == segments[0])
            ?? throw new ArgumentException($"point '{point.Id}' references unknown mapping '{segments[0]}'", nameof(point));

        if (!mapping.Aliases.Contains(segments[1], StringComparer.Ordinal))
        {
            throw new ArgumentException($"point '{point.Id}' references unknown alias '{segments[1]}'", nameof(point));
        }

        return new PointMappingResolution(source, mapping, segments[1], point);
    }
}