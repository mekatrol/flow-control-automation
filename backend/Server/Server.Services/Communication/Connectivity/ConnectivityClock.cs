namespace Server.Services.Communication.Connectivity;

internal sealed class ConnectivityClock(TimeProvider timeProvider) : IConnectivityClock
{
    public DateTimeOffset UtcNow => timeProvider.GetUtcNow();
}