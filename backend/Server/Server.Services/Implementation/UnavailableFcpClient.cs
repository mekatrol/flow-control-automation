namespace Server.Services.Implementation;

internal sealed class UnavailableFcpClient : IFcpClient
{
    public Task<ReadOnlyMemory<byte>> ExchangeAuthenticatedAsync(
        byte operation,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken) =>
        throw new ControllerGatewayException(
            "transport",
            "No controller transport is configured.");
}