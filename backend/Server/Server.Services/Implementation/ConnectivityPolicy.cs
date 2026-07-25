using System.Net;
using System.Net.Sockets;

namespace Server.Services.Implementation;

internal static class ConnectivityPolicy
{
    public static bool IsForbidden(IPAddress address, bool allowPrivateNetwork)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address)
            || address.Equals(IPAddress.Any)
            || address.Equals(IPAddress.IPv6Any)
            || address.Equals(IPAddress.Broadcast)
            || address.IsIPv6Multicast
            || address.IsIPv6LinkLocal
            || address.IsIPv6SiteLocal)
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var ipv6Bytes = address.GetAddressBytes();
            var documentationRange = ipv6Bytes[0] == 0x20
                && ipv6Bytes[1] == 0x01
                && ipv6Bytes[2] == 0x0d
                && ipv6Bytes[3] == 0xb8;
            return documentationRange
                || (address.IsIPv6UniqueLocal && !allowPrivateNetwork);
        }

        var bytes = address.GetAddressBytes();
        var privateAddress = bytes[0] == 10
            || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
            || (bytes[0] == 192 && bytes[1] == 168);
        var alwaysForbidden = bytes[0] == 0
            || bytes[0] == 127
            || bytes[0] >= 224
            || (bytes[0] == 100 && bytes[1] is >= 64 and <= 127)
            || (bytes[0] == 169 && bytes[1] == 254)
            || (bytes[0] == 192 && bytes[1] == 0 && bytes[2] is 0 or 2)
            || (bytes[0] == 198 && bytes[1] is 18 or 19 or 51)
            || (bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113);
        return alwaysForbidden || (privateAddress && !allowPrivateNetwork);
    }
}