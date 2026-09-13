using Server.Data.Context;
using System.Text.Json;

namespace Server.Services.Points.Definitions;

/// <summary>Read-only point catalogue projected from persisted source aggregates.</summary>
internal sealed class PointDefinitionDatabaseStore(IFlowControlDbContext context) : IPointDefinitionStore
{
    public async Task<IReadOnlyList<AutomationPoint>> ListPointsAsync(
        CancellationToken cancellationToken) =>
        [.. (await Sources(cancellationToken))
            .SelectMany(source => source.Points)
            .OrderBy(point => point.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(point => point.Id, StringComparer.Ordinal)];

    public async Task<AutomationPoint> GetPointAsync(
        string id,
        CancellationToken cancellationToken)
    {
        var ownership = await context.PointSourcePoints.AsNoTracking()
            .SingleOrDefaultAsync(item => item.PointId == id, cancellationToken)
            ?? throw new PointDefinitionNotFoundException("point", id);

        var entity = await context.PointSources.AsNoTracking()
            .SingleAsync(source => source.Id == ownership.SourceId, cancellationToken);
        var source = JsonSerializer.Deserialize<PointSource>(entity.Json, FlowControlJson.Options)
            ?? throw new InvalidOperationException($"Stored point source {entity.Id} is null.");

        return source.Points.Single(point => point.Id == id);
    }

    private async Task<IReadOnlyList<PointSource>> Sources(CancellationToken cancellationToken) =>
        [.. (await context.PointSources.AsNoTracking().ToListAsync(cancellationToken))
            .Select(entity => JsonSerializer.Deserialize<PointSource>(entity.Json, FlowControlJson.Options)
                ?? throw new InvalidOperationException($"Stored point source {entity.Id} is null."))];
}
