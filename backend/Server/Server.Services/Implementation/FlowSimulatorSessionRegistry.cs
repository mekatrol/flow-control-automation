namespace Server.Services.Implementation;

public sealed class FlowSimulatorSessionRegistry : IDisposable
{
    public const int MaximumSessions = 32;
    public static readonly TimeSpan DefaultLease = TimeSpan.FromSeconds(3);
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _lease;

    public FlowSimulatorSessionRegistry(TimeProvider timeProvider)
        : this(timeProvider, DefaultLease)
    {
    }

    public FlowSimulatorSessionRegistry(TimeProvider timeProvider, TimeSpan lease)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lease, TimeSpan.Zero);
        _timeProvider = timeProvider;
        _lease = lease;
    }

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

            var entry = new Entry(flowId, registry, _timeProvider.GetUtcNow(), emulatorId, cleanup);
            _entries.Add(flowId, entry);
            entry.ScheduleExpiry(_timeProvider, _lease, () => Remove(flowId, entry));
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
            entry.LastAccess = _timeProvider.GetUtcNow();
            entry.ScheduleExpiry(_timeProvider, _lease, () => Remove(entry.FlowId, entry));
            return checked((uint)_lease.TotalMilliseconds);
        }
    }

    private void RemoveExpiredCore()
    {
        var now = _timeProvider.GetUtcNow();
        foreach (var pair in _entries.Where(pair => now - pair.Value.LastAccess >= _lease).ToArray())
        {
            _entries.Remove(pair.Key);
            pair.Value.Dispose();
        }
    }

    public void Dispose()
    {
        Clear();
    }

    internal void Clear()
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

    internal sealed class Entry(string flowId, FlowDebugSessionRegistry registry, DateTimeOffset lastAccess, string? emulatorId, Action? cleanup) : IDisposable
    {
        private CancellationTokenSource? _continuousCancellation;
        private ITimer? _expiryTimer;
        private long _expiryVersion;
        public string FlowId { get; } = flowId;
        public FlowDebugSessionRegistry Registry { get; } = registry;
        public string? EmulatorId { get; } = emulatorId;
        public DateTimeOffset LastAccess { get; set; } = lastAccess;
        public void StartContinuous(Func<CancellationToken, Task> scan, uint intervalMilliseconds)
        {
            StopContinuous();
            _continuousCancellation = new CancellationTokenSource();
            var token = _continuousCancellation.Token;
            _ = Task.Run(async () =>
            {
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        await Task.Delay(checked((int)Math.Max(1, intervalMilliseconds)), token);
                        await scan(token);
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            }, CancellationToken.None);
        }
        public void StopContinuous()
        {
            _continuousCancellation?.Cancel();
            _continuousCancellation?.Dispose();
            _continuousCancellation = null;
        }
        public void ScheduleExpiry(TimeProvider timeProvider, TimeSpan lease, Action expire)
        {
            var version = Interlocked.Increment(ref _expiryVersion);
            _expiryTimer?.Dispose();
            _expiryTimer = timeProvider.CreateTimer(
                static state =>
                {
                    var expiry = (ExpiryState)state!;
                    if (Volatile.Read(ref expiry.Entry._expiryVersion) == expiry.Version)
                    {
                        expiry.Expire();
                    }
                },
                new ExpiryState(this, version, expire),
                lease,
                Timeout.InfiniteTimeSpan);
        }
        public void Dispose()
        {
            Interlocked.Increment(ref _expiryVersion);
            _expiryTimer?.Dispose();
            _expiryTimer = null;
            StopContinuous();
            Registry.Dispose();
            cleanup?.Invoke();
        }

        private sealed record ExpiryState(Entry Entry, long Version, Action Expire);
    }
}