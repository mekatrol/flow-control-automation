using Server.Common.Contracts;

namespace Server.Services;

/// <summary>Compiles and transactionally activates flows on their resolved execution targets.</summary>
public interface IFlowDeploymentService
{
    /// <summary>Builds and validates a replacement runtime before atomically publishing it for the supplied flow.</summary>
    /// <param name="flow">The saved current-schema flow to deploy; its ID and positive revision identify the deployment being replaced.</param>
    /// <param name="cancellationToken">Cancels compilation or staging; cancellation must leave the currently active runtime unchanged.</param>
    /// <returns>The first immutable snapshot of the newly active runtime.</returns>
    Task<RuntimeSnapshot> DeployAsync(Flow flow, CancellationToken cancellationToken);
}