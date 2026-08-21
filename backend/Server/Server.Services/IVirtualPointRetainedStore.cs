namespace Server.Services;

public interface IVirtualPointRetainedStore
{
    Task<RetainedVirtualPointValue?> ReadAsync(
        string executionInstanceId,
        string pointKey,
        CancellationToken cancellationToken);

    Task WriteAsync(
        string executionInstanceId,
        IReadOnlyDictionary<string, RetainedVirtualPointValue> values,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, RetainedVirtualPointValue>> ListAsync(string executionInstanceId, CancellationToken cancellationToken);
    Task ReplaceAsync(string executionInstanceId, IReadOnlyDictionary<string, RetainedVirtualPointValue> values, CancellationToken cancellationToken);
    Task ClearAsync(string executionInstanceId, CancellationToken cancellationToken);
}