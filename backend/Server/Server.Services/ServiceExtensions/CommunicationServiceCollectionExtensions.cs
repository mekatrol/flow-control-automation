using Server.Services.Communication.Connectivity;
using Server.Services.Communication.Controllers;
using Server.Services.Communication.Fcp;
using Server.Services.Communication.Network;
using Server.Services.Communication.Protocols.Http;
using Server.Services.Communication.Protocols.Mqtt;

namespace Server.Services.ServiceExtensions;

internal static class CommunicationServiceCollectionExtensions
{
    internal static IServiceCollection AddCommunicationServices(this IServiceCollection services)
    {
        services.AddSingleton<IFcpClient, UnavailableFcpClient>();
        services.AddSingleton<IControllerDebugTransport, FcpControllerDebugTransport>();
        services.AddSingleton<IConnectivityClock, ConnectivityClock>();
        services.AddSingleton<ConnectivityRateLimiter>();
        services.AddSingleton<IDnsLookup, DnsLookup>();
        services.AddSingleton<ITcpConnectionFactory, TcpConnectionFactory>();
        services.AddSingleton<ITlsHandshake, TlsHandshake>();
        services.AddSingleton<IHttpProtocolCheck, HttpProtocolCheck>();
        services.AddSingleton<IMqttProtocolCheck, MqttProtocolCheck>();
        services.AddScoped<IConnectivityService, ConnectivityService>();

        return services;
    }
}