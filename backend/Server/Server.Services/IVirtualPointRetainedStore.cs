using Server.Services.Contracts;

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
}