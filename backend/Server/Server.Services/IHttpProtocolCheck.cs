using Server.Services.Contracts;
using System.Net;

namespace Server.Services;

public interface IHttpProtocolCheck
{
    Task<string?> CheckAsync(
        PointSource source,
        string credential,
        IReadOnlyList<IPAddress> pinnedAddresses,
        CancellationToken cancellationToken);
}