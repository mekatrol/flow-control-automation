namespace Server.Services;

/// <summary>Validates durable application data before request processing begins.</summary>
public interface IStartupDataValidator
{
    /// <summary>Validates all startup-owned data and fails startup when persisted contracts are malformed or unsupported.</summary>
    /// <param name="cancellationToken">Signals that host startup is being abandoned; cancellation must stop validation promptly without modifying persisted data.</param>
    /// <returns>A task that completes only after every configured data source has passed validation.</returns>
    Task ValidateAsync(CancellationToken cancellationToken);
}