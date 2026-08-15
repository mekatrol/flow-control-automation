namespace Server.Services.Implementation;

public sealed class FlowSimulatorSessionRegistry(TimeProvider timeProvider) : IDisposable
{
    public const int MaximumSessions = 32;
    public static readonly TimeSpan Lease = TimeSpan.FromMinutes(15);
    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    internal Entry? Get(string flowId)
    {
        lock (_gate)
        {
            RemoveExpiredCore();
            return _entries.GetValueOrDefault(flowId);
        }
    }

    internal Entry Add(string flowId, FlowDebugSessionRegistry registry, bool replaceExisting, string? emulatorId = null, Action? cleanup = null)
    {
        lock (_gate)
        {
            RemoveExpiredCore();
            if (_entries.TryGetValue(flowId, out var existing))
            {
                if (!replaceExisting)
                {
                    throw new FlowSimulatorException("simulator_session_conflict", "A simulator session already exists for this flow.");
                }

                existing.Dispose();
                _entries.Remove(flowId);
            }
            if (_entries.Count >= MaximumSessions)
            {
                throw new FlowSimulatorException("simulator_limit_exceeded", "The active simulator session limit has been reached.");
            }

            var entry = new Entry(registry, timeProvider.GetUtcNow(), emulatorId, cleanup);
            _entries.Add(flowId, entry);
            return entry;
        }
    }

    internal bool Remove(string flowId, string sessionId)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(flowId, out var entry)
                || !string.Equals(entry.Registry.Session?.DebugSessionId, sessionId, StringComparison.Ordinal))
            {
                return false;
            }

            _entries.Remove(flowId);
            entry.Dispose();
            return true;
        }
    }

    internal void Remove(string flowId, Entry expected)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(flowId, out var entry) || !ReferenceEquals(entry, expected))
            {
                return;
            }

            _entries.Remove(flowId);
            entry.Dispose();
        }
    }

    internal uint Touch(Entry entry)
    {
        lock (_gate)
        {
            entry.LastAccess = timeProvider.GetUtcNow();
            return checked((uint)Lease.TotalMilliseconds);
        }
    }

    private void RemoveExpiredCore()
    {
        var now = timeProvider.GetUtcNow();
        foreach (var pair in _entries.Where(pair => now - pair.Value.LastAccess >= Lease).ToArray())
        {
            _entries.Remove(pair.Key);
            pair.Value.Dispose();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var entry in _entries.Values)
            {
                entry.Dispose();
            }

            _entries.Clear();
        }
    }

    internal sealed class Entry(FlowDebugSessionRegistry registry, DateTimeOffset lastAccess, string? emulatorId, Action? cleanup) : IDisposable
    {
        public FlowDebugSessionRegistry Registry { get; } = registry;
        public string? EmulatorId { get; } = emulatorId;
        public DateTimeOffset LastAccess { get; set; } = lastAccess;
        public void Dispose()
        {
            Registry.Dispose();
            cleanup?.Invoke();
        }
    }
}