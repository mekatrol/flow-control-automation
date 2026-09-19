namespace Server.Common.Contracts.FlowExecution;

public interface IFlowExecutionMetadataStore
{
    Task RecordSuccessfulExecutionAsync(
        string flowId,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken);
}