using System.Net;

namespace Server.Services;

public interface IDnsLookup
{
    Task<IReadOnlyList<IPAddress>> LookupAsync(
        string host,
        CancellationToken cancellationToken);
}