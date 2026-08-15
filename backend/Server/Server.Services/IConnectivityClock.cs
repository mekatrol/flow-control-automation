namespace Server.Services;

/// <summary>Provides wall-clock time to connectivity operations so timeout and latency behavior can be tested deterministically.</summary>
public interface IConnectivityClock
{
    /// <summary>Gets the current UTC instant; implementations must return an offset of zero and must not move backward during one operation.</summary>
    DateTimeOffset UtcNow { get; }
}