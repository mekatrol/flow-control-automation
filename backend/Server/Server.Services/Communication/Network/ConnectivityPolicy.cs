using System.Net;

namespace Server.Services.Communication.Network;

internal static class ConnectivityPolicy
{
    public static bool IsForbidden(IPAddress address, bool allowPrivateNetwork)
        => Server.Common.Models.Communication.ConnectivityPolicy.IsForbidden(
            address,
            allowPrivateNetwork);
}