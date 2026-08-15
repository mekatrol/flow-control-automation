using System.Net;

namespace Server.Services;

/// <summary>Resolves host names for bounded connectivity checks and SSRF validation.</summary>
public interface IDnsLookup
{
    /// <summary>Resolves every address currently advertised for a host without opening a network connection.</summary>
    /// <param name="host">A non-empty DNS host name or IP literal accepted by the point-source contract; URI syntax and port suffixes are not allowed.</param>
    /// <param name="cancellationToken">Cancels name resolution when the connectivity-test budget expires or the caller aborts.</param>
    /// <returns>A deterministic, duplicate-free list of resolved IPv4 or IPv6 addresses; an empty list represents no DNS answers.</returns>
    Task<IReadOnlyList<IPAddress>> LookupAsync(
        string host,
        CancellationToken cancellationToken);
}