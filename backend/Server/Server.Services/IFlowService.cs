using Server.Common.Contracts;

namespace Server.Services;

/// <summary>Provides revision-safe durable CRUD operations for editable flow definitions.</summary>
public interface IFlowService
{
    /// <summary>Lists flows using deterministic filtering, sorting, and pagination.</summary>
    /// <param name="options">Validated list options; page and page size must be positive and sort/filter values must use the supported vocabulary.</param>
    /// <param name="cancellationToken">Cancels the read without changing flow state.</param>
    /// <returns>The requested page and total matching count; items may be empty.</returns>
    Task<PaginatedResult<Flow>> ListAsync(
        FlowListOptions options,
        CancellationToken cancellationToken);

    /// <summary>Gets the current revision of one flow.</summary>
    /// <param name="id">The non-empty canonical flow identifier.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The complete stored flow.</returns>
    Task<Flow> GetAsync(string id, CancellationToken cancellationToken);

    /// <summary>Creates an empty editable flow with a generated canonical ID.</summary>
    /// <param name="name">The non-empty display name after trimming, within flow name-length limits.</param>
    /// <param name="cancellationToken">Cancels before the atomic create commits.</param>
    /// <returns>The new flow with its initial positive revision and timestamps.</returns>
    Task<Flow> CreateAsync(string name, CancellationToken cancellationToken);

    /// <summary>Validates and atomically replaces an existing flow revision.</summary>
    /// <param name="id">The non-empty route ID, which must equal <paramref name="flow"/>'s ID.</param>
    /// <param name="flow">The complete replacement carrying the currently stored revision for optimistic concurrency.</param>
    /// <param name="cancellationToken">Cancels before the replacement commits.</param>
    /// <returns>The persisted flow with its incremented revision and updated timestamp.</returns>
    Task<Flow> SaveAsync(string id, Flow flow, CancellationToken cancellationToken);

    /// <summary>Records the current validated draft as the version accepted by the runtime.</summary>
    Task<Flow> MarkDeployedAsync(string id, int revision, CancellationToken cancellationToken);

    /// <summary>Replaces the editable draft content with the last deployed snapshot.</summary>
    Task<Flow> RevertToDeployedAsync(string id, CancellationToken cancellationToken);

    /// <summary>Changes only a flow's disabled state under optimistic concurrency.</summary>
    /// <param name="id">The non-empty canonical flow identifier.</param>
    /// <param name="disabled">The desired execution-disabled state.</param>
    /// <param name="cancellationToken">Cancels before the state change commits.</param>
    /// <returns>The updated flow with an incremented revision.</returns>
    Task<Flow> SetDisabledAsync(
        string id,
        bool disabled,
        CancellationToken cancellationToken);

    /// <summary>Deletes a flow and its owned durable resources after reference checks pass.</summary>
    /// <param name="id">The non-empty canonical flow identifier.</param>
    /// <param name="cancellationToken">Cancels before deletion commits.</param>
    /// <returns>A task that completes when the flow is no longer stored.</returns>
    Task DeleteAsync(string id, CancellationToken cancellationToken);
}
