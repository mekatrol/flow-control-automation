using Server.Services.Contracts;

namespace Server.Services;

public interface IFlowPointAdapter
{
    Task<IReadOnlyList<FlowVmInput>> ReadAsync(
        IReadOnlyList<string> pointIds,
        CancellationToken cancellationToken);

    Task PublishAsync(
        string flowId,
        IReadOnlyList<FlowVmCommand> commands,
        CancellationToken cancellationToken);
}
