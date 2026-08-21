using Server.Common.Contracts;
using Server.Data.Context;
using System.Text.Json;

namespace Server.Services.Implementation;

internal sealed class VirtualPointMigrationService(
    IFlowControlDbContext context,
    TimeProvider timeProvider) : IVirtualPointMigrationService
{
    public async Task<VirtualPointMigrationReport> RunAsync(bool apply, CancellationToken cancellationToken)
    {
        var pointEntities = await context.Points.AsNoTracking().ToListAsync(cancellationToken);
        var points = pointEntities.Select(entity => Deserialize<FlowPoint>(entity.Json))
            .ToDictionary(point => point.Id, StringComparer.Ordinal);
        var flows = await context.Flows.ToListAsync(cancellationToken);
        var diagnostics = new List<VirtualPointMigrationDiagnostic>();
        var changed = 0;
        var added = 0;

        foreach (var entity in flows)
        {
            var flow = Deserialize<Flow>(entity.Json);
            var declarations = flow.VirtualPointDeclarations.ToDictionary(item => item.Key, StringComparer.Ordinal);
            var before = declarations.Count;
            foreach (var node in flow.Nodes)
            {
                if (node.Kind is not (FlowNodeKind.AnalogInput or FlowNodeKind.AnalogOutput or FlowNodeKind.DigitalInput or FlowNodeKind.DigitalOutput)
                    || !node.Configuration.TryGetValue("pointId", out var pointIdValue)
                    || pointIdValue.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var pointId = pointIdValue.GetString()!;
                if (!points.TryGetValue(pointId, out var point))
                {
                    diagnostics.Add(new(flow.Id, "point_not_found", "error", $"Point '{pointId}' does not exist.", node.Id, pointId));
                    continue;
                }
                if (!string.Equals(point.Implementation, "virtual", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var expected = node.Kind is FlowNodeKind.AnalogInput or FlowNodeKind.AnalogOutput ? FlowPointValueType.Analog : FlowPointValueType.Digital;
                if (point.ValueType != expected)
                {
                    diagnostics.Add(new(flow.Id, "point_type_mismatch", "error", $"Point '{pointId}' has the wrong value type for the node.", node.Id, pointId));
                    continue;
                }

                var declaration = new VirtualPointDeclaration
                {
                    Key = point.Id,
                    ValueType = point.ValueType,
                    Units = point.Units,
                    Readable = point.Readable,
                    Commandable = point.Commandable,
                    Persistence = string.Equals(point.Persistence, "retained", StringComparison.OrdinalIgnoreCase)
                        ? VirtualPointPersistence.Retained : VirtualPointPersistence.Volatile,
                    RelinquishDefault = point.RelinquishDefault is null
                        ? null : JsonSerializer.SerializeToElement(point.RelinquishDefault, FlowControlJson.Options)
                };
                if (declarations.TryGetValue(point.Id, out var existing) && existing != declaration)
                {
                    diagnostics.Add(new(flow.Id, "declaration_conflict", "error", $"Point '{pointId}' conflicts with the flow declaration.", node.Id, pointId));
                    continue;
                }
                declarations.TryAdd(point.Id, declaration);
            }

            var count = declarations.Count - before;
            if (count == 0)
            {
                continue;
            }

            changed++;
            added += count;
            diagnostics.Add(new(flow.Id, "declarations_inferred", "info", $"Inferred {count} virtual point declaration(s)."));
            if (apply)
            {
                var migrated = flow with
                {
                    Revision = checked(flow.Revision + 1),
                    VirtualPointDeclarations = [.. declarations.Values.OrderBy(item => item.Key, StringComparer.Ordinal)]
                };
                entity.Json = JsonSerializer.Serialize(migrated, FlowControlJson.Options);
                entity.Updated = timeProvider.GetUtcNow();
            }
        }

        if (apply && changed > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return new(apply, flows.Count, changed, added, diagnostics);
    }

    private static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, FlowControlJson.Options)
        ?? throw new InvalidOperationException($"Stored {typeof(T).Name} is null.");
}