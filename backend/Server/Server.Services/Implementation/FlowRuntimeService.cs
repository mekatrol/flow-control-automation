using Server.Services.Contracts;
using System.Collections.Concurrent;
using System.Globalization;

namespace Server.Services.Implementation;

internal sealed class FlowRuntimeService(TimeProvider timeProvider) : IFlowRuntimeService
{
    private readonly ConcurrentDictionary<string, RuntimeSnapshot> _snapshots = [];

    public RuntimeSnapshot Get(Flow flow)
    {
        ArgumentNullException.ThrowIfNull(flow);
        return flow.Disabled
            ? Stop(flow)
            : _snapshots.GetValueOrDefault(flow.Id)
                ?? CreateSnapshot(flow, "stopped");
    }

    public RuntimeSnapshot Deploy(Flow flow)
    {
        ArgumentNullException.ThrowIfNull(flow);
        if (flow.Disabled)
        {
            return Stop(flow);
        }

        var snapshot = CreateSnapshot(flow, "running");
        _snapshots[flow.Id] = snapshot;
        return snapshot;
    }

    public RuntimeSnapshot Stop(Flow flow)
    {
        ArgumentNullException.ThrowIfNull(flow);
        var snapshot = CreateSnapshot(flow, "stopped");
        _snapshots[flow.Id] = snapshot;
        return snapshot;
    }

    public void Delete(string flowId) => _snapshots.TryRemove(flowId, out _);

    private RuntimeSnapshot CreateSnapshot(Flow flow, string state)
    {
        var updatedAt = timeProvider.GetUtcNow().ToString(
            "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK",
            CultureInfo.InvariantCulture);
        var nodes = flow.Nodes.ToDictionary(
            node => node.Id,
            _ => new NodeRuntimeSnapshot(state, updatedAt),
            StringComparer.Ordinal);
        return new RuntimeSnapshot(flow.Id, state, updatedAt, nodes);
    }
}