using System.Collections.Concurrent;

namespace Server.Services.FlowExecution.ExecutionContext;

internal sealed class FlowDebugSuspensionCoordinator(
    IFlowDebugLeaseRepository leases,
    IFlowRuntimeService runtime,
    IFlowDeploymentService deployment) : IFlowDebugSuspensionCoordinator
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.Ordinal);

    public async Task<FlowDebugLease> AcquireAsync(
        Flow flow,
        string contextId,
        CancellationToken cancellationToken)
    {
        var gate = Gates.GetOrAdd(flow.Id, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);

        try
        {
            var suspended = flow.DeployedVersion is not null && !flow.Disabled;
            var lease = await leases.AcquireAsync(flow.Id, contextId, suspended, cancellationToken);

            if (suspended)
            {
                runtime.Stop(flow);
            }

            return lease;
        }
        catch
        {
            await leases.ReleaseAsync(flow.Id, contextId, CancellationToken.None);
            throw;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<bool> ReleaseAsync(
        Flow flow,
        string contextId,
        CancellationToken cancellationToken)
    {
        var gate = Gates.GetOrAdd(flow.Id, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);

        try
        {
            var lease = await leases.GetByFlowAsync(flow.Id, cancellationToken);

            if (lease is null || !string.Equals(lease.ExecutionContextId, contextId, StringComparison.Ordinal))
            {
                return false;
            }

            if (lease.SuspendedEnabledDeployment && !flow.Disabled && flow.DeployedVersion is not null)
            {
                await deployment.DeployAsync(DeployedFlow(flow), cancellationToken);
            }

            return await leases.ReleaseAsync(flow.Id, contextId, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task EnsureAvailableAsync(string flowId, CancellationToken cancellationToken)
    {
        if (await leases.GetByFlowAsync(flowId, cancellationToken) is not null)
        {
            throw new FlowExecutionContextConflictException("The flow is temporarily disabled while it is being debugged.");
        }
    }

    private static Flow DeployedFlow(Flow flow)
    {
        var version = flow.DeployedVersion!;

        return flow with
        {
            Name = version.Name,
            Description = version.Description,
            UpdatedAt = version.UpdatedAt,
            Revision = version.Revision,
            Nodes = version.Nodes,
            Connections = version.Connections,
            Status = "deployed"
        };
    }
}