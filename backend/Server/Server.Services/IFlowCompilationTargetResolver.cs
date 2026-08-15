using Server.Services.Contracts;

namespace Server.Services;

/// <summary>Resolves an executable flow's pinned dependencies into one immutable compilation target.</summary>
public interface IFlowCompilationTargetResolver
{
    /// <summary>Loads the exact controller-template and point revisions referenced by a flow snapshot.</summary>
    /// <param name="source">The validated current-schema executable source containing non-empty dependency IDs and positive pinned revisions.</param>
    /// <param name="cancellationToken">Cancels dependency reads before compilation starts.</param>
    /// <returns>A self-consistent target containing the validated template and deterministically ordered point definitions.</returns>
    Task<FlowCompilationTarget> ResolveAsync(
        ExecutableFlowSource source,
        CancellationToken cancellationToken);
}