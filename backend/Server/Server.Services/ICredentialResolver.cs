namespace Server.Services;

/// <summary>Resolves opaque credential references to secret values at the last responsible moment.</summary>
public interface ICredentialResolver
{
    /// <summary>Retrieves the secret associated with a stored credential reference without exposing unrelated metadata.</summary>
    /// <param name="reference">The non-empty opaque reference previously issued by the credential store; raw secret values are not accepted.</param>
    /// <param name="cancellationToken">Cancels the lookup before the secret is returned.</param>
    /// <returns>The exact stored secret value; it is non-null and may be empty only when the credential contract explicitly permits an empty secret.</returns>
    Task<string> ResolveAsync(
        string reference,
        CancellationToken cancellationToken);
}