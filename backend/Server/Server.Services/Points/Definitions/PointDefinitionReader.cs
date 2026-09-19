using Server.Data.Context;
using System.Text.Json;

namespace Server.Services.Points.Definitions;

/// <summary>Queries points projected from persisted source aggregates.</summary>
internal sealed class PointDefinitionReader(IFlowControlDbContext context) : IPointDefinitionReader
{
    public async Task<IReadOnlyList<AutomationPoint>> ListPointsAsync(
        CancellationToken cancellationToken) =>
        [.. (await Sources(cancellationToken))
            .SelectMany(source => source.Points.Select(point => Project(source, point)))
            .OrderBy(point => point.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(point => point.Id, StringComparer.Ordinal)];

    public async Task<AutomationPoint> GetPointAsync(
        string sourceId,
        string pointId,
        CancellationToken cancellationToken)
    {
        var ownership = await context.PointSourcePoints.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.SourceId == sourceId && item.PointId == pointId,
                cancellationToken)
            ?? throw new PointDefinitionNotFoundException("point", $"{sourceId}/{pointId}");

        var entity = await context.PointSources.AsNoTracking()
            .SingleAsync(source => source.Id == ownership.SourceId, cancellationToken);
        var source = JsonSerializer.Deserialize<PointSource>(entity.Json, FlowControlJson.Options)
            ?? throw new InvalidOperationException($"Stored point source {entity.Id} is null.");

        return Project(source, source.Points.Single(point => point.Id == pointId));
    }

    private static AutomationPoint Project(PointSource source, AutomationPoint point) => point with
    {
        SourceId = source.Id,
        SourceName = source.Name,
        SourceKind = source.Kind
    };

    private async Task<IReadOnlyList<PointSource>> Sources(CancellationToken cancellationToken) =>
        [.. (await context.PointSources.AsNoTracking().ToListAsync(cancellationToken))
            .Select(entity => JsonSerializer.Deserialize<PointSource>(entity.Json, FlowControlJson.Options)
                ?? throw new InvalidOperationException($"Stored point source {entity.Id} is null."))];
}