using Server.Common.Errors;
using Server.Data.Context;
using Server.Data.Entities;

namespace Server.Services.FlowExecution.ExecutionContext;

internal sealed class FlowDebugLeaseRepository(
    IFlowControlDbContext context,
    TimeProvider timeProvider) : IFlowDebugLeaseRepository
{
    public async Task<FlowDebugLease> AcquireAsync(
        string flowId,
        string executionContextId,
        bool suspendedEnabledDeployment,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(flowId);
        ArgumentException.ThrowIfNullOrWhiteSpace(executionContextId);
        var now = timeProvider.GetUtcNow();
        var entity = new FlowDebugLeaseEntity
        {
            FlowId = flowId,
            ExecutionContextId = executionContextId,
            StartedAt = now,
            LastHeartbeatAt = now,
            SuspendedEnabledDeployment = suspendedEnabledDeployment
        };
        context.FlowDebugLeases.Add(entity);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraint(exception))
        {
            throw new FlowDebugLeaseConflictException(flowId, exception);
        }

        return Map(entity);
    }

    public async Task<FlowDebugLease?> GetByFlowAsync(
        string flowId,
        CancellationToken cancellationToken)
    {
        var entity = await context.FlowDebugLeases
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.FlowId == flowId, cancellationToken);

        return entity is null ? null : Map(entity);
    }

    public async Task HeartbeatAsync(
        string flowId,
        string executionContextId,
        CancellationToken cancellationToken)
    {
        var entity = await MatchingLease(flowId, executionContextId, cancellationToken);

        if (entity is null)
        {
            return;
        }

        entity.LastHeartbeatAt = timeProvider.GetUtcNow();
        await context.SaveChangesAsync(cancellationToken);
        await context.ReloadAsync(entity, cancellationToken);
    }

    public async Task<bool> ReleaseAsync(
        string flowId,
        string executionContextId,
        CancellationToken cancellationToken)
    {
        var entity = await MatchingLease(flowId, executionContextId, cancellationToken);

        if (entity is null)
        {
            return false;
        }

        context.FlowDebugLeases.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<IReadOnlyList<FlowDebugLease>> ReleaseExpiredAsync(
        DateTimeOffset heartbeatCutoff,
        CancellationToken cancellationToken)
    {
        // SQLite cannot translate ordering comparisons for DateTimeOffset. The
        // table contains at most one short-lived row per actively debugged flow,
        // so materialize before applying the backend-clock cutoff.
        var leases = await context.FlowDebugLeases.ToListAsync(cancellationToken);
        var expired = leases
            .Where(item => item.LastHeartbeatAt < heartbeatCutoff)
            .ToList();

        if (expired.Count == 0)
        {
            return [];
        }

        var released = expired.Select(Map).ToArray();
        context.FlowDebugLeases.RemoveRange(expired);
        await context.SaveChangesAsync(cancellationToken);

        return released;
    }

    private Task<FlowDebugLeaseEntity?> MatchingLease(
        string flowId,
        string executionContextId,
        CancellationToken cancellationToken) =>
        context.FlowDebugLeases.SingleOrDefaultAsync(
            item => item.FlowId == flowId && item.ExecutionContextId == executionContextId,
            cancellationToken);

    private static FlowDebugLease Map(FlowDebugLeaseEntity entity) => new(
        entity.FlowId,
        entity.ExecutionContextId,
        entity.StartedAt,
        entity.LastHeartbeatAt,
        entity.SuspendedEnabledDeployment,
        entity.RowVersion);

    private static bool IsUniqueConstraint(DbUpdateException exception) =>
        exception.InnerException?.Message.Contains(
            "UNIQUE constraint failed",
            StringComparison.Ordinal) == true;
}