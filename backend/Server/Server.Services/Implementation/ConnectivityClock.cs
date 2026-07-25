namespace Server.Services.Implementation;

internal sealed class ConnectivityClock(TimeProvider timeProvider) : IConnectivityClock
{
    public DateTimeOffset UtcNow => timeProvider.GetUtcNow();
}