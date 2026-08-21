using Server.Common.Contracts;
using Server.Compiler.Contracts;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;

namespace Server.Services.Implementation;

internal sealed class FlowRuntimeService(
    TimeProvider timeProvider,
    IFlowVirtualMachineFactory machines,
    IFlowPointAdapter points,
    IVirtualPointRuntimeStore virtualPoints) : IFlowRuntimeService, IDisposable
{
    public FlowRuntimeService(
        TimeProvider timeProvider,
        IFlowVirtualMachineFactory machines,
        IFlowPointAdapter points)
        : this(timeProvider, machines, points, new VirtualPointRuntimeStore(timeProvider))
    {
    }

    private readonly ConcurrentDictionary<string, RuntimeInstance> _instances = [];
    private readonly ConcurrentDictionary<string, RuntimeSnapshot> _snapshots = [];
    private readonly CancellationTokenSource _shutdown = new();
    private readonly SemaphoreSlim _deploymentGate = new(1, 1);
    private bool _disposed;

    public RuntimeSnapshot Get(Flow flow)
    {
        ArgumentNullException.ThrowIfNull(flow);
        return flow.Disabled
            ? Stop(flow)
            : _snapshots.GetValueOrDefault(flow.Id) ?? Snapshot(flow, "stopped");
    }

    public async Task<RuntimeSnapshot> DeployAsync(
        Flow flow,
        FlowCompilationResult compilation,
        IReadOnlyList<string> inputPointIds,
        TimeSpan interval,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(flow);
        ArgumentNullException.ThrowIfNull(compilation);
        if (flow.Disabled)
        {
            return Stop(flow);
        }

        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval));
        }

        await _deploymentGate.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var writerKeys = flow.Nodes
                .Where(node => node.Kind is FlowNodeKind.AnalogOutput or FlowNodeKind.DigitalOutput)
                .Select(node => node.Configuration.TryGetValue("pointId", out var value) ? value.GetString() : null)
                .Where(key => key is not null && flow.VirtualPointDeclarations.Any(item => item.Key == key))
                .Select(key => key!)
                .ToHashSet(StringComparer.Ordinal);
            await virtualPoints.ActivateFlowAsync("server", flow.Id, flow.VirtualPointDeclarations, writerKeys, cancellationToken);
            var machine = machines.Create(compilation.Artifact);
            var replacement = new RuntimeInstance(
                flow,
                compilation,
                inputPointIds,
                interval,
                machine,
                CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token));
            if (_instances.TryRemove(flow.Id, out var previous))
            {
                await StopInstanceAsync(previous);
            }

            if (!_instances.TryAdd(flow.Id, replacement))
            {
                replacement.Dispose();
                throw new InvalidOperationException("Unable to activate the runtime instance.");
            }

            var initial = Snapshot(flow, "running");
            _snapshots[flow.Id] = initial;
            replacement.Task = RunAsync(replacement);
            return initial;
        }
        finally
        {
            _deploymentGate.Release();
        }
    }

    public RuntimeSnapshot Stop(Flow flow)
    {
        ArgumentNullException.ThrowIfNull(flow);
        _deploymentGate.Wait();
        try
        {
            if (_instances.TryRemove(flow.Id, out var instance))
            {
                instance.Cancellation.Cancel();
                try
                {
                    instance.Task.Wait(TimeSpan.FromSeconds(5));
                }
                catch (AggregateException exception) when (exception.InnerExceptions.All(item => item is OperationCanceledException))
                {
                }
                finally
                {
                    instance.DrainScans(TimeSpan.FromSeconds(5));
                    instance.Dispose();
                }
            }

            virtualPoints.ReleaseFlow("server", flow.Id);

            return _snapshots[flow.Id] = Snapshot(flow, "stopped");
        }
        finally
        {
            _deploymentGate.Release();
        }
    }

    public async Task<RuntimeSnapshot> ScanOnceAsync(
        Flow flow,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(flow);
        if (!_instances.TryGetValue(flow.Id, out var instance))
        {
            throw new InvalidOperationException("The flow is not deployed.");
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            instance.Cancellation.Token);
        await ExecuteScanAsync(instance, linked.Token);
        return _snapshots[flow.Id];
    }

    public void Delete(string flowId)
    {
        if (_instances.TryRemove(flowId, out var instance))
        {
            instance.Cancellation.Cancel();
            instance.DrainScans(TimeSpan.FromSeconds(5));
            instance.Dispose();
        }
        virtualPoints.ReleaseFlow("server", flowId);
        _snapshots.TryRemove(flowId, out _);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _shutdown.Cancel();
        var instances = _instances.Values.ToArray();
        _instances.Clear();
        foreach (var instance in instances)
        {
            instance.Cancellation.Cancel();
        }

        try
        {
            Task.WaitAll([.. instances.Select(instance => instance.Task)], TimeSpan.FromSeconds(5));
        }
        catch (AggregateException exception) when (exception.InnerExceptions.All(item => item is OperationCanceledException))
        {
        }
        foreach (var instance in instances)
        {
            instance.DrainScans(TimeSpan.FromSeconds(5));
            instance.Dispose();
            virtualPoints.ReleaseFlow("server", instance.Flow.Id);
        }
        _shutdown.Dispose();
        _deploymentGate.Dispose();
    }

    private async Task RunAsync(RuntimeInstance instance)
    {
        try
        {
            while (true)
            {
                instance.Cancellation.Token.ThrowIfCancellationRequested();
                await ExecuteScanAsync(instance, instance.Cancellation.Token);

                await Task.Delay(instance.Interval, timeProvider, instance.Cancellation.Token);
            }
        }
        catch (OperationCanceledException) when (instance.Cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _snapshots[instance.Flow.Id] = Snapshot(instance.Flow, "faulted") with
            {
                Diagnostic = exception.Message
            };
        }
    }

    private async Task ExecuteScanAsync(RuntimeInstance instance, CancellationToken cancellationToken)
    {
        await instance.ScanGate.WaitAsync(cancellationToken);
        try
        {
            var readTimer = Stopwatch.StartNew();
            var inputs = await points.ReadAsync(instance.InputPointIds, cancellationToken);
            readTimer.Stop();

            var executeTimer = Stopwatch.StartNew();
            var scan = instance.Machine.Scan(inputs, MonotonicMilliseconds());
            executeTimer.Stop();

            var writeTimer = Stopwatch.StartNew();
            await points.PublishAsync(instance.Flow.Id, scan.Commands, cancellationToken);
            writeTimer.Stop();

            var updatedAt = Timestamp();
            var nodes = instance.Flow.Nodes.ToDictionary(
                node => node.Id,
                node => new NodeRuntimeSnapshot("running", updatedAt)
                {
                    TypedValue = instance.Compilation.NodeIndices.TryGetValue(node.Id, out var slot)
                        && slot < scan.Slots.Count
                            ? scan.Slots[slot]
                            : null,

                    Value = instance.Compilation.NodeIndices.TryGetValue(node.Id, out slot)
                        && slot < scan.Slots.Count
                        && scan.Slots[slot].DataType == DataType.Boolean
                            ? scan.Slots[slot].Boolean
                            : null
                },
                StringComparer.Ordinal);

            _snapshots[instance.Flow.Id] = new RuntimeSnapshot(
                instance.Flow.Id,
                "running",
                updatedAt,
                nodes)
            {
                ScanNumber = scan.ScanNumber,
                ReadInputsMilliseconds = readTimer.Elapsed.TotalMilliseconds,
                ExecuteLogicMilliseconds = executeTimer.Elapsed.TotalMilliseconds,
                WriteOutputsMilliseconds = writeTimer.Elapsed.TotalMilliseconds,
                Outputs = scan.Commands.ToDictionary(command => command.PointId, command => command.Value, StringComparer.Ordinal)
            };
        }
        finally
        {
            instance.ScanGate.Release();
        }
    }

    private async Task StopInstanceAsync(RuntimeInstance instance)
    {
        instance.Cancellation.Cancel();
        try
        {
            await instance.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (OperationCanceledException)
        {
        }
        catch (TimeoutException)
        {
        }
        finally
        {
            instance.DrainScans(TimeSpan.FromSeconds(5));
            instance.Dispose();
        }
    }

    private RuntimeSnapshot Snapshot(Flow flow, string state)
    {
        var updatedAt = Timestamp();
        return new RuntimeSnapshot(
            flow.Id,
            state,
            updatedAt,
            flow.Nodes.ToDictionary(
                node => node.Id,
                _ => new NodeRuntimeSnapshot(state, updatedAt),
                StringComparer.Ordinal));
    }

    private string Timestamp() => timeProvider.GetUtcNow().ToString(
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK",
        CultureInfo.InvariantCulture);

    private ulong MonotonicMilliseconds() => checked((ulong)timeProvider
        .GetElapsedTime(0, timeProvider.GetTimestamp())
        .TotalMilliseconds);

    private sealed class RuntimeInstance(
        Flow flow,
        FlowCompilationResult compilation,
        IReadOnlyList<string> inputPointIds,
        TimeSpan interval,
        IFlowVirtualMachine machine,
        CancellationTokenSource cancellation) : IDisposable
    {
        public Flow Flow { get; } = flow;
        public FlowCompilationResult Compilation { get; } = compilation;
        public IReadOnlyList<string> InputPointIds { get; } = inputPointIds;
        public TimeSpan Interval { get; } = interval;
        public IFlowVirtualMachine Machine { get; } = machine;
        public CancellationTokenSource Cancellation { get; } = cancellation;
        public SemaphoreSlim ScanGate { get; } = new(1, 1);
        public Task Task { get; set; } = Task.CompletedTask;
        private bool IsDisposed { get; set; }

        public void DrainScans(TimeSpan timeout)
        {
            if (!ScanGate.Wait(timeout))
            {
                throw new TimeoutException("A PLC scan did not stop within the shutdown bound.");
            }

            ScanGate.Release();
        }

        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;
            Machine.Dispose();
            ScanGate.Dispose();
            Cancellation.Dispose();
        }
    }
}
