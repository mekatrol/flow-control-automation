using Server.Services.Contracts;

namespace Server.Services;

public interface IFlowCompilationTargetResolver
{
    Task<FlowCompilationTarget> ResolveAsync(
        ExecutableFlowSource source,
        CancellationToken cancellationToken);
}
