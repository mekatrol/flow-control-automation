namespace Server.Services;

public interface IConnectivityClock
{
    DateTimeOffset UtcNow { get; }
}