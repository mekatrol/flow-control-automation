#pragma warning disable IDE0011, CC0001, CC0002, CC0003
using System.Collections.Concurrent;

namespace Server.Services.FlowExecution.ExecutionContext;

internal sealed class FlowExecutionContextRegistry(TimeProvider timeProvider) : IDisposable
{
    internal sealed class Entry : IDisposable
    {
        private int _disposed;

        public required string Id { get; init; }
        public required string FlowId { get; init; }
        public required uint Revision { get; init; }
        public required FlowExecutionMode Mode { get; init; }
        public required string TargetId { get; init; }
        public IFlowDebugService? Debug { get; init; }
        public IFlowSimulatorService? Simulator { get; init; }
        public required string SessionId { get; init; }
        public required FlowExecutionContext Context { get; set; }
        public required DateTimeOffset LastAccess { get; set; }
        public SemaphoreSlim Gate { get; } = new(1, 1);

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

            try
            {
                if (Simulator is not null)
                {
                    Simulator.StopAsync(FlowId, SessionId, CancellationToken.None).GetAwaiter().GetResult();
                }
                else if (Debug is not null)
                {
                    Debug.StopAsync(FlowId, SessionId, CancellationToken.None).GetAwaiter().GetResult();
                }
            }
            catch (Exception exception)
            {
                // Expiration and replacement cleanup is best effort and idempotent.
                _ = exception;
            }

            Gate.Dispose();
            if (Debug is IDisposable disposable) disposable.Dispose();
        }

        public void DisposeAfterCleanup()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

            Gate.Dispose();
            if (Debug is IDisposable disposable) disposable.Dispose();
        }
    }

    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(15);
    private readonly ConcurrentDictionary<string, Entry> _entries = [];

    public Entry Add(Entry entry, bool replaceExisting)
    {
        var existing = _entries.Values.FirstOrDefault(value =>
            string.Equals(value.FlowId, entry.FlowId, StringComparison.Ordinal)
            && value.Context.Lifecycle != FlowExecutionLifecycle.Stopped);
        if (existing is not null && !replaceExisting)
            throw new FlowExecutionContextConflictException("An active execution context already exists for this flow.");
        if (existing is not null) Remove(existing.Id)?.Dispose();
        if (!_entries.TryAdd(entry.Id, entry)) throw new InvalidOperationException("Unable to register execution context.");
        return entry;
    }

    public Entry Get(string id)
    {
        if (!_entries.TryGetValue(id, out var entry)) throw new FlowExecutionContextNotFoundException(id);
        entry.LastAccess = timeProvider.GetUtcNow();
        return entry;
    }

    public Entry? Remove(string id) => _entries.TryRemove(id, out var entry) ? entry : null;

    public Entry? Find(string id) => _entries.GetValueOrDefault(id);

    public IReadOnlyList<string> ActiveIds() => [.. _entries
        .Where(pair => pair.Value.Context.Lifecycle != FlowExecutionLifecycle.Stopped)
        .Select(pair => pair.Key)];

    public IReadOnlyList<Entry> TakeExpired()
    {
        var threshold = timeProvider.GetUtcNow() - Lease;

        return [.. _entries
            .Where(pair => pair.Value.LastAccess <= threshold
                && pair.Value.Context.Lifecycle != FlowExecutionLifecycle.Stopped)
            .Select(pair => Remove(pair.Key))
            .Where(entry => entry is not null)
            .Select(entry => entry!)];
    }

    public uint Remaining(Entry entry)
    {
        var remaining = Lease - (timeProvider.GetUtcNow() - entry.LastAccess);
        return remaining <= TimeSpan.Zero ? 0u : checked((uint)Math.Min(uint.MaxValue, remaining.TotalMilliseconds));
    }

    public void Dispose()
    {
        foreach (var id in _entries.Keys.ToArray()) Remove(id)?.Dispose();
    }
}