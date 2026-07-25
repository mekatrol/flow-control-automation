namespace Server.Services;

public interface ICredentialResolver
{
    Task<string> ResolveAsync(
        string reference,
        CancellationToken cancellationToken);
}