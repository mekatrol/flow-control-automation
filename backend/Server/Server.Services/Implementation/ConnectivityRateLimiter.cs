namespace Server.Services.Implementation;

internal sealed class ConnectivityRateLimiter
{
    private const int MaximumTests = 10;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
    private readonly Lock _lock = new();
    private readonly Dictionary<string, List<DateTimeOffset>> _recent = [];

    public bool Allow(string key, DateTimeOffset now)
    {
        lock (_lock)
        {
            var cutoff = now - Window;
            if (!_recent.TryGetValue(key, out var entries))
            {
                entries = [];
                _recent[key] = entries;
            }

            entries.RemoveAll(item => item <= cutoff);
            if (entries.Count >= MaximumTests)
            {
                return false;
            }

            entries.Add(now);
            return true;
        }
    }
}