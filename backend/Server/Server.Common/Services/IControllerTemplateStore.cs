using Server.Common.Contracts;

namespace Server.Common.Services;

/// <summary>Persists validated controller templates with revision safety and built-in-template protection.</summary>
public interface IControllerTemplateStore
{
    /// <summary>Lists templates deterministically, including the immutable built-in default.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A non-empty list when the built-in default is available.</returns>
    Task<IReadOnlyList<ControllerTemplate>> ListAsync(CancellationToken cancellationToken);
    /// <summary>Gets one template by canonical ID.</summary>
    /// <param name="id">The non-empty template ID.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The current complete template.</returns>
    Task<ControllerTemplate> GetAsync(string id, CancellationToken cancellationToken);
    /// <summary>Validates and creates a custom template.</summary>
    /// <param name="template">A complete current-schema template with no reserved built-in identity and an unset initial revision.</param>
    /// <param name="cancellationToken">Cancels before creation commits.</param>
    /// <returns>The persisted template with its initial positive revision.</returns>
    Task<ControllerTemplate> CreateAsync(
        ControllerTemplate template,
        CancellationToken cancellationToken);
    /// <summary>Validates and atomically replaces a custom template revision.</summary>
    /// <param name="id">The non-empty route ID, which must match the replacement template ID.</param>
    /// <param name="template">The complete replacement template.</param>
    /// <param name="revision">The positive stored revision expected by the caller.</param>
    /// <param name="cancellationToken">Cancels before replacement commits.</param>
    /// <returns>The persisted template with an incremented revision.</returns>
    Task<ControllerTemplate> UpdateAsync(
        string id,
        ControllerTemplate template,
        int revision,
        CancellationToken cancellationToken);
    /// <summary>Deletes an unreferenced custom template; the built-in default cannot be deleted.</summary>
    /// <param name="id">The non-empty custom template ID.</param>
    /// <param name="revision">The positive currently observed revision.</param>
    /// <param name="cancellationToken">Cancels before deletion commits.</param>
    /// <returns>A task that completes when the template is removed.</returns>
    Task DeleteAsync(string id, int revision, CancellationToken cancellationToken);
}