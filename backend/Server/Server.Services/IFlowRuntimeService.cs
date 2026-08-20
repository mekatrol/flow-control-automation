using Server.Common.Contracts;
using Server.Compiler.Contracts;

namespace Server.Services;

public interface IFlowRuntimeService
{
    RuntimeSnapshot Get(Flow flow);

    Task<RuntimeSnapshot> DeployAsync(
        Flow flow,
        FlowCompilationResult compilation,
        IReadOnlyList<string> inputPointIds,
        TimeSpan interval,
        CancellationToken cancellationToken);

    Task<RuntimeSnapshot> ScanOnceAsync(Flow flow, CancellationToken cancellationToken);

    RuntimeSnapshot Stop(Flow flow);

    void Delete(string flowId);
}