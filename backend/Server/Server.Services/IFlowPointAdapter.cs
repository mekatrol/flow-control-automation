namespace Server.Services;

/// <summary>Bridges portable VM point slots to authoritative server point I/O at PLC scan boundaries.</summary>
public interface IFlowPointAdapter
{
    /// <summary>Freezes the requested input values for one scan before logic execution begins.</summary>
    /// <param name="pointIds">Distinct, non-empty readable point IDs in compiler slot order; the list may be empty for flows without inputs.</param>
    /// <param name="cancellationToken">Cancels source reads before an input snapshot is returned.</param>
    /// <returns>Exactly one typed input per requested ID in the same order, including quality metadata for unavailable values.</returns>
    Task<IReadOnlyList<FlowVmInput>> ReadAsync(
        IReadOnlyList<string> pointIds,
        CancellationToken cancellationToken);

    /// <summary>Publishes a successful scan's staged output commands as one host-level commit.</summary>
    /// <param name="flowId">The non-empty flow ID used for command attribution and isolation.</param>
    /// <param name="commands">Commands in deterministic compiler order; point IDs must be distinct and commandable, and the collection may be empty.</param>
    /// <param name="cancellationToken">Cancels publication before completion; implementations must not expose a partially successful scan as committed.</param>
    /// <returns>A task that completes after all accepted commands have been published.</returns>
    Task PublishAsync(
        string flowId,
        IReadOnlyList<FlowVmCommand> commands,
        CancellationToken cancellationToken);
}