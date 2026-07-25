using Server.Services.Contracts;

namespace Server.Services;

public interface IMqttProtocolCheck
{
    Task<string?> CheckAsync(
        Stream stream,
        PointSource source,
        string credential,
        CancellationToken cancellationToken);
}