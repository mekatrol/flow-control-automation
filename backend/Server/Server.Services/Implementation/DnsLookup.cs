using System.Net;

namespace Server.Services.Implementation;

internal sealed class DnsLookup : IDnsLookup
{
    public async Task<IReadOnlyList<IPAddress>> LookupAsync(
        string host,
        CancellationToken cancellationToken) =>
        await Dns.GetHostAddressesAsync(host, cancellationToken);
}