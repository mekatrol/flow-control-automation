using Server.Compiler.Contracts;

namespace Server.Services;

internal interface IFlowRuntimeDeploymentService
{
    Task<RuntimeSnapshot> DeployAsync(
        Flow flow,
        FlowCompilationResult compilation,
        IReadOnlyList<string> inputPointIds,
        TimeSpan interval,
        CancellationToken cancellationToken);
}