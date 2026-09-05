using Server.Common.Models;
using Server.Common.Types;
using Server.Compiler.Contracts;
using Server.Compiler.Services;
using System.Collections.Concurrent;

namespace Server.Services.Implementation;

public sealed class FlowEmulatorService : IFlowEmulatorService, IDisposable
{
    private const int MaximumHistory = 1024;
    public const int MaximumInstances = 32;
    public static readonly TimeSpan Lease = TimeSpan.FromMinutes(15);
    private readonly ConcurrentDictionary<string, Instance> _instances = [];
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly IFlowCompilationTargetResolver? _targetResolver;
    private readonly IFlowCompiler _compiler;
    private readonly IFlowVirtualMachineFactory _machines;
    private readonly TimeProvider _timeProvider;
    private readonly Lock _gate = new();

    [ActivatorUtilitiesConstructor]
    public FlowEmulatorService(
        IServiceScopeFactory scopeFactory,
        IFlowCompiler compiler,
        IFlowVirtualMachineFactory machines,
        TimeProvider timeProvider)
    {
        _scopeFactory = scopeFactory;
        _compiler = compiler;
        _machines = machines;
        _timeProvider = timeProvider;
    }

    public FlowEmulatorService(
        IFlowCompilationTargetResolver targetResolver,
        IFlowCompiler compiler,
        IFlowVirtualMachineFactory machines)
        : this(targetResolver, compiler, machines, TimeProvider.System)
    {
    }

    public FlowEmulatorService(
        IFlowCompilationTargetResolver targetResolver,
        IFlowCompiler compiler,
        IFlowVirtualMachineFactory machines,
        TimeProvider timeProvider)
    {
        _targetResolver = targetResolver;
        _compiler = compiler;
        _machines = machines;
        _timeProvider = timeProvider;
    }

    public async Task<FlowEmulatorSnapshot> CreateAsync(
        ExecutableFlowSource source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        RemoveExpired();
        Compiler.Contracts.FlowCompilationTarget target;
        if (_scopeFactory is not null)
        {
            using var scope = _scopeFactory.CreateScope();
            target = await scope.ServiceProvider
                .GetRequiredService<IFlowCompilationTargetResolver>()
                .ResolveAsync(source, cancellationToken);
        }
        else
        {
            target = await _targetResolver!.ResolveAsync(source, cancellationToken);
        }
        var compilation = _compiler.Compile(new FlowCompilationRequest { Source = source, Target = target });
        var id = Guid.NewGuid().ToString("N");
        var instance = new Instance(id, source, _machines.Create(compilation.Artifact), _timeProvider.GetUtcNow());
        lock (_gate)
        {
            RemoveExpiredCore();
            if (_instances.Count >= MaximumInstances)
            {
                instance.Dispose();
                throw new FlowSimulatorException("simulator_limit_exceeded", "The active emulator limit has been reached.");
            }
            if (!_instances.TryAdd(id, instance))
            {
                instance.Dispose();
                throw new InvalidOperationException("Unable to allocate an emulator instance.");
            }
        }
        return instance.Snapshot();
    }

    public FlowEmulatorSnapshot Get(string emulatorId) => GetInstance(emulatorId).Snapshot();

    public FlowEmulatorSnapshot SetInputs(string emulatorId, IReadOnlyList<EmulatorInputChange> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        return GetInstance(emulatorId).SetInputs(changes);
    }

    public FlowEmulatorSnapshot ApplyInputsAndStep(string emulatorId, IReadOnlyList<EmulatorInputChange> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        return GetInstance(emulatorId).ApplyInputsAndStep(changes);
    }

    public FlowEmulatorSnapshot Advance(string emulatorId, ulong milliseconds, bool scan) =>
        GetInstance(emulatorId).Advance(milliseconds, scan);

    public FlowEmulatorSnapshot InjectFault(string emulatorId, string? fault) =>
        GetInstance(emulatorId).InjectFault(fault);

    public FlowEmulatorSnapshot Reset(string emulatorId, bool powerCycle) =>
        GetInstance(emulatorId).Reset(powerCycle);

    public FlowEmulatorSnapshot ResetInputs(string emulatorId) => GetInstance(emulatorId).ResetInputs();

    public void Delete(string emulatorId)
    {
        if (_instances.TryRemove(emulatorId, out var instance))
        {
            instance.Dispose();
        }
    }

    public void Dispose()
    {
        foreach (var instance in _instances.Values)
        {
            instance.Dispose();
        }

        _instances.Clear();
    }

    internal Instance GetInstance(string emulatorId)
    {
        RemoveExpired();
        var instance = _instances.GetValueOrDefault(emulatorId)
            ?? throw new FlowEmulatorNotFoundException(emulatorId);
        instance.LastAccess = _timeProvider.GetUtcNow();
        return instance;
    }

    private void RemoveExpired()
    {
        lock (_gate)
        {
            RemoveExpiredCore();
        }
    }

    private void RemoveExpiredCore()
    {
        var now = _timeProvider.GetUtcNow();
        foreach (var pair in _instances.Where(pair => now - pair.Value.LastAccess >= Lease).ToArray())
        {
            if (_instances.TryRemove(pair.Key, out var expired))
            {
                expired.Dispose();
            }
        }
    }

    internal sealed class Instance : IDisposable
    {
        private readonly Lock _gate = new();
        private readonly ExecutableFlowSource _source;
        private readonly IFlowVirtualMachine _machine;
        private readonly Dictionary<string, FlowVmInput> _inputs = new(StringComparer.Ordinal);
        private readonly List<EmulatorInputChange> _pending = [];
        private readonly List<EmulatorOutputSample> _outputs = [];
        private string? _fault;
        private ulong _clock;
        private ulong _scanNumber;

        public Instance(string id, ExecutableFlowSource source, IFlowVirtualMachine machine, DateTimeOffset lastAccess)
        {
            Id = id;
            _source = source;
            _machine = machine;
            LastAccess = lastAccess;
            foreach (var input in InitialInputs(source))
            {
                _inputs[input.PointId] = input;
            }
        }

        public string Id { get; }
        public DateTimeOffset LastAccess { get; set; }

        public FlowEmulatorSnapshot SetInputs(IReadOnlyList<EmulatorInputChange> changes)
        {
            lock (_gate)
            {
                QueueInputs(changes);
                return SnapshotCore();
            }
        }

        public FlowEmulatorSnapshot ApplyInputsAndStep(IReadOnlyList<EmulatorInputChange> changes)
        {
            lock (_gate)
            {
                QueueInputs(changes);
                ScanCore();
                return SnapshotCore();
            }
        }

        public FlowEmulatorSnapshot Advance(ulong milliseconds, bool scan)
        {
            lock (_gate)
            {
                _clock = checked(_clock + milliseconds);
                if (scan)
                {
                    ScanCore();
                }

                return SnapshotCore();
            }
        }

        public FlowEmulatorSnapshot InjectFault(string? fault)
        {
            if (fault is not (null or "communication_loss" or "stale_input" or "output_failure" or "reset" or "power_cycle"))
            {
                throw new ArgumentException("Unsupported emulator fault.", nameof(fault));
            }
            lock (_gate)
            {
                _fault = fault;
                if (fault is "reset" or "power_cycle")
                {
                    ResetCore();
                }

                return SnapshotCore();
            }
        }

        public FlowEmulatorSnapshot Reset(bool powerCycle)
        {
            lock (_gate)
            {
                ResetCore();
                if (powerCycle)
                {
                    _clock = 0;
                }

                _fault = null;
                return SnapshotCore();
            }
        }

        public FlowEmulatorSnapshot ResetInputs()
        {
            lock (_gate)
            {
                _pending.Clear();
                _inputs.Clear();
                foreach (var input in InitialInputs(_source))
                {
                    _inputs[input.PointId] = input;
                }

                return SnapshotCore();
            }
        }

        public FlowEmulatorSnapshot Snapshot()
        {
            lock (_gate)
            {
                return SnapshotCore();
            }
        }

        internal IReadOnlyList<FlowVmInput> CaptureInputs()
        {
            lock (_gate)
            {
                ApplyPendingInputs();
                return [.. _inputs.Values.OrderBy(input => input.PointId, StringComparer.Ordinal)];
            }
        }

        internal ulong Clock
        {
            get
            {
                lock (_gate)
                {
                    return _clock;
                }
            }
        }

        internal void Publish(FlowVmScanResult scan)
        {
            lock (_gate)
            {
                PublishCore(scan);
            }
        }

        private void ScanCore()
        {
            ApplyPendingInputs();
            if (_fault == "communication_loss")
            {
                foreach (var input in _inputs.Values.ToArray())
                {
                    _inputs[input.PointId] = new FlowVmInput(
                        input.PointId,
                        input.TypedValue with { Quality = DataQualityType.Unavailable });
                }
            }
            var scan = _machine.Scan([.. _inputs.Values.OrderBy(input => input.PointId, StringComparer.Ordinal)], _clock);
            PublishCore(scan);
        }

        private void PublishCore(FlowVmScanResult scan)
        {
            _scanNumber = scan.ScanNumber;
            foreach (var command in scan.Commands)
            {
                var failed = _fault == "output_failure";
                var quality = failed ? DataQualityType.Bad : command.TypedValue.Quality;
                var effective = failed ? command.TypedValue with { Quality = DataQualityType.Bad } : command.TypedValue;

                var previous = _outputs.LastOrDefault(output => output.OutputId == command.PointId);

                var lastChange = previous is null || previous.EffectiveValue != effective
                    ? scan.ScanNumber
                    : previous.LastChangeScan;

                _outputs.Add(new EmulatorOutputSample(
                    scan.ScanNumber, _clock, command.PointId, command.TypedValue, effective,
                    quality, null, lastChange, "emulator", 16, null));
            }
            if (_outputs.Count > MaximumHistory)
            {
                _outputs.RemoveRange(0, _outputs.Count - MaximumHistory);
            }
        }

        private void ApplyPendingInputs()
        {
            var ready = _pending.TakeWhile(change => change.EffectiveAtMilliseconds <= _clock).ToArray();
            foreach (var change in ready)
            {
                var existing = _inputs[change.InputId];
                _inputs[change.InputId] = new FlowVmInput(
                    change.InputId,
                    change.TypedValue);
            }
            _pending.RemoveRange(0, ready.Length);
        }

        private void ResetCore()
        {
            _machine.Reset();
            _scanNumber = 0;
            _outputs.Clear();
            _pending.Clear();
            _inputs.Clear();
            foreach (var input in InitialInputs(_source))
            {
                _inputs[input.PointId] = input;
            }
        }

        private void QueueInputs(IReadOnlyList<EmulatorInputChange> changes)
        {
            foreach (var change in changes)
            {
                if (!_inputs.TryGetValue(change.InputId, out var existing))
                {
                    throw new ArgumentException($"Input '{change.InputId}' is not mapped by this flow.", nameof(changes));
                }

                if (change.TypedValue.DataType != existing.TypedValue.DataType)
                {
                    throw new ArgumentException($"Input '{change.InputId}' requires type '{existing.TypedValue.DataType}'.", nameof(changes));
                }

                if (change.TypedValue.Quality is not (
                        DataQualityType.Good or
                        DataQualityType.Bad or
                        DataQualityType.Uncertain or
                        DataQualityType.Unavailable)
                    )
                {
                    throw new ArgumentException($"Input '{change.InputId}' has unsupported quality.", nameof(changes));
                }

                if (change.TypedValue.DataType == DataType.Number && !double.IsFinite(change.TypedValue.Number))
                {
                    throw new ArgumentException($"Input '{change.InputId}' must be finite.", nameof(changes));
                }

                var scheduled = change with { EffectiveAtMilliseconds = change.EffectiveAtMilliseconds ?? _clock };
                _pending.Add(scheduled);
            }
            _pending.Sort(static (left, right) => Nullable.Compare(left.EffectiveAtMilliseconds, right.EffectiveAtMilliseconds));
        }

        private FlowEmulatorSnapshot SnapshotCore() => new()
        {
            EmulatorId = Id,
            FlowId = _source.Id,
            ControllerTemplateId = _source.ControllerTemplateId,
            LifecycleState = _fault is null ? "ready" : "fault-injected",
            VirtualTimeMilliseconds = _clock,
            ScanNumber = _scanNumber,
            Inputs = [.. _inputs.Values.OrderBy(input => input.PointId, StringComparer.Ordinal)],
            OutputHistory = [.. _outputs],
            ActiveFault = _fault
        };

        private static IEnumerable<FlowVmInput> InitialInputs(ExecutableFlowSource source) => source.Nodes
            .Where(node => node.NodeType is FlowNodeType.DigitalInput or FlowNodeType.AnalogInput
                && node.Configuration.TryGetValue("pointId", out _))
            .Select(node => new
            {
                PointId = node.Configuration["pointId"].GetString(),
                node.NodeType
            })
            .Where(static item => !string.IsNullOrEmpty(item.PointId))
            .DistinctBy(static item => item.PointId, StringComparer.Ordinal)
            .Select(static item => new FlowVmInput(
                item.PointId!,
                item.NodeType == FlowNodeType.AnalogInput
                    ? FlowVmValue.FromNumber(0)
                    : FlowVmValue.FromBoolean(false)));

        public void Dispose() => _machine.Dispose();
    }
}