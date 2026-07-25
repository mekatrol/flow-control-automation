using Server.Services.Contracts;

namespace Server.Services;

public interface ICredentialStore
{
    Task<IReadOnlyList<CredentialMetadata>> ListAsync(
        CancellationToken cancellationToken);

    Task<CredentialMetadata> GetAsync(
        string id,
        CancellationToken cancellationToken);

    Task<CredentialMetadata> CreateAsync(
        CredentialInput input,
        CancellationToken cancellationToken);

    Task<CredentialMetadata> UpdateAsync(
        string id,
        CredentialInput input,
        CancellationToken cancellationToken);

    Task DeleteAsync(string id, int revision, CancellationToken cancellationToken);
}