using Server.Data.Context;
using Server.Data.Entities;
using System.Text.Json;

namespace Server.Services.Implementation;

internal sealed class VirtualPointRetainedDatabaseStore(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider) : IVirtualPointRetainedStore
{
    public async Task<RetainedVirtualPointValue?> ReadAsync(
        string executionInstanceId,
        string pointKey,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IFlowControlDbContext>();
        var entity = await context.VirtualPointRetainedStates.AsNoTracking().SingleOrDefaultAsync(
            item => item.ExecutionInstanceId == executionInstanceId && item.PointKey == pointKey,
            cancellationToken);
        return entity is null
            ? null
            : JsonSerializer.Deserialize<RetainedVirtualPointValue>(entity.Json, FlowControlJson.Options)
                ?? throw new InvalidOperationException("Stored retained virtual-point value is null.");
    }

    public async Task WriteAsync(
        string executionInstanceId,
        IReadOnlyDictionary<string, RetainedVirtualPointValue> values,
        CancellationToken cancellationToken)
    {
        if (values.Count == 0)
        {
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IFlowControlDbContext>();
        var pointKeys = values.Keys.ToArray();
        var existing = await context.VirtualPointRetainedStates
            .Where(item => item.ExecutionInstanceId == executionInstanceId && pointKeys.Contains(item.PointKey))
            .ToDictionaryAsync(item => item.PointKey, StringComparer.Ordinal, cancellationToken);
        var now = timeProvider.GetUtcNow();
        foreach (var (pointKey, value) in values)
        {
            if (!existing.TryGetValue(pointKey, out var entity))
            {
                var identity = $"{executionInstanceId}:{pointKey}";
                entity = new VirtualPointRetainedStateEntity
                {
                    Id = identity,
                    Key = identity,
                    ExecutionInstanceId = executionInstanceId,
                    PointKey = pointKey,
                    Created = now
                };
                context.VirtualPointRetainedStates.Add(entity);
            }

            entity.Json = JsonSerializer.Serialize(value, FlowControlJson.Options);
            entity.Updated = now;
        }
        await context.SaveChangesAsync(cancellationToken);
    }
}