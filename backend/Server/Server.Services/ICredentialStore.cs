using Server.Services.Contracts;

namespace Server.Services;

/// <summary>Stores encrypted credentials while exposing only non-secret metadata to callers.</summary>
public interface ICredentialStore
{
    /// <summary>Lists credential metadata in deterministic order without decrypting or returning secrets.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A possibly empty list containing no secret values.</returns>
    Task<IReadOnlyList<CredentialMetadata>> ListAsync(
        CancellationToken cancellationToken);

    /// <summary>Gets metadata for one credential without exposing its secret.</summary>
    /// <param name="id">The non-empty opaque credential ID.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The credential's current metadata and positive revision.</returns>
    Task<CredentialMetadata> GetAsync(
        string id,
        CancellationToken cancellationToken);

    /// <summary>Encrypts and stores a new credential.</summary>
    /// <param name="input">The name, kind, and secret satisfying credential length and vocabulary limits.</param>
    /// <param name="cancellationToken">Cancels before encrypted data commits.</param>
    /// <returns>Metadata for the new credential; the secret is never echoed.</returns>
    Task<CredentialMetadata> CreateAsync(
        CredentialInput input,
        CancellationToken cancellationToken);

    /// <summary>Atomically replaces credential metadata and, when supplied, its encrypted secret.</summary>
    /// <param name="id">The non-empty ID of the credential being updated.</param>
    /// <param name="input">The complete validated replacement and current revision.</param>
    /// <param name="cancellationToken">Cancels before replacement commits.</param>
    /// <returns>Updated metadata with an incremented revision.</returns>
    Task<CredentialMetadata> UpdateAsync(
        string id,
        CredentialInput input,
        CancellationToken cancellationToken);

    /// <summary>Deletes an unreferenced credential using optimistic concurrency.</summary>
    /// <param name="id">The non-empty credential ID.</param>
    /// <param name="revision">The positive revision currently observed by the caller.</param>
    /// <param name="cancellationToken">Cancels before deletion commits.</param>
    /// <returns>A task that completes when encrypted secret material and metadata are removed.</returns>
    Task DeleteAsync(string id, int revision, CancellationToken cancellationToken);
}