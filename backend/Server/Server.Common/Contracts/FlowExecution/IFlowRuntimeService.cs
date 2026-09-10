namespace Server.Common.Contracts.FlowExecution;

public interface IFlowRuntimeService
{
    RuntimeSnapshot Get(Flow flow);

    Task<RuntimeSnapshot> ScanOnceAsync(Flow flow, CancellationToken cancellationToken);

    RuntimeSnapshot Stop(Flow flow);

    void Delete(string flowId);
}