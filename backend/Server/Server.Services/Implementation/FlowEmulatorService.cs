using Server.Services.Contracts;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace Server.Services.Implementation;

public sealed class FlowEmulatorService : IFlowEmulatorService, IDisposable
{
    private const int MaximumHistory = 1024;
    private readonly ConcurrentDictionary<string, Instance> _instances = [];
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly IFlowCompilationTargetResolver? _targetResolver;
    private readonly IFlowCompiler _compiler;
    private readonly IFlowVirtualMachineFactory _machines;

    [ActivatorUtilitiesConstructor]
    public FlowEmulatorService(
        IServiceScopeFactory scopeFactory,
        IFlowCompiler compiler,
        IFlowVirtualMachineFactory machines)
    {
        _scopeFactory = scopeFactory;
        _compiler = compiler;
        _machines = machines;
    }

    public FlowEmulatorService(
        IFlowCompilationTargetResolver targetResolver,
        IFlowCompiler compiler,
        IFlowVirtualMachineFactory machines)
    {
        _targetResolver = targetResolver;
        _compiler = compiler;
        _machines = machines;
    }

    public async Task<FlowEmulatorSnapshot> CreateAsync(
        ExecutableFlowSource source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        FlowCompilationTarget target;
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
        var instance = new Instance(id, source, _machines.Create(compilation.Artifact));
        if (!_instances.TryAdd(id, instance))
        {
            instance.Dispose();
            throw new InvalidOperationException("Unable to allocate an emulator instance.");
        }
        return instance.Snapshot();
    }

    public FlowEmulatorSnapshot Get(string emulatorId) => GetInstance(emulatorId).Snapshot();

    public FlowEmulatorSnapshot SetInputs(string emulatorId, IReadOnlyList<EmulatorInputChange> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        return GetInstance(emulatorId).SetInputs(changes);
    }

    public FlowEmulatorSnapshot Advance(string emulatorId, ulong milliseconds, bool scan) =>
        GetInstance(emulatorId).Advance(milliseconds, scan);

    public FlowEmulatorSnapshot InjectFault(string emulatorId, string? fault) =>
        GetInstance(emulatorId).InjectFault(fault);

    public FlowEmulatorSnapshot Reset(string emulatorId, bool powerCycle) =>
        GetInstance(emulatorId).Reset(powerCycle);

    public FlowEmulatorScenario ExportScenario(string emulatorId) => GetInstance(emulatorId).ExportScenario();

    public void Delete(string emulatorId)
    {
        if (_instances.TryRemove(emulatorId, out var instance)) instance.Dispose();
    }

    public void Dispose()
    {
        foreach (var instance in _instances.Values) instance.Dispose();
        _instances.Clear();
    }

    internal Instance GetInstance(string emulatorId) => _instances.GetValueOrDefault(emulatorId)
        ?? throw new FlowEmulatorNotFoundException(emulatorId);

    internal sealed class Instance : IDisposable
    {
        private readonly object _gate = new();
        private readonly ExecutableFlowSource _source;
        private readonly IFlowVirtualMachine _machine;
        private readonly Dictionary<string, FlowVmInput> _inputs = new(StringComparer.Ordinal);
        private readonly List<EmulatorInputChange> _pending = [];
        private readonly List<EmulatorInputChange> _scenarioInputs = [];
        private readonly List<EmulatorOutputSample> _outputs = [];
        private string? _fault;
        private ulong _clock;
        private ulong _scanNumber;

        public Instance(string id, ExecutableFlowSource source, IFlowVirtualMachine machine)
        {
            Id = id;
            _source = source;
            _machine = machine;
            foreach (var pointId in InputPointIds(source)) _inputs[pointId] = new FlowVmInput(pointId, false);
        }

        public string Id { get; }

        public FlowEmulatorSnapshot SetInputs(IReadOnlyList<EmulatorInputChange> changes)
        {
            lock (_gate)
            {
                foreach (var change in changes)
                {
                    if (!_inputs.ContainsKey(change.PointId))
                    {
                        throw new ArgumentException($"Input point '{change.PointId}' is not mapped by this flow.", nameof(changes));
                    }
                    var scheduled = change with { EffectiveAtMilliseconds = change.EffectiveAtMilliseconds ?? _clock };
                    _pending.Add(scheduled);
                    _scenarioInputs.Add(scheduled);
                }
                _pending.Sort(static (left, right) => Nullable.Compare(left.EffectiveAtMilliseconds, right.EffectiveAtMilliseconds));
                return SnapshotCore();
            }
        }

        public FlowEmulatorSnapshot Advance(ulong milliseconds, bool scan)
        {
            lock (_gate)
            {
                _clock = checked(_clock + milliseconds);
                if (scan) ScanCore();
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
                if (fault is "reset" or "power_cycle") ResetCore();
                return SnapshotCore();
            }
        }

        public FlowEmulatorSnapshot Reset(bool powerCycle)
        {
            lock (_gate)
            {
                ResetCore();
                if (powerCycle) _clock = 0;
                _fault = null;
                return SnapshotCore();
            }
        }

        public FlowEmulatorScenario ExportScenario()
        {
            lock (_gate) return new FlowEmulatorScenario([.. _scenarioInputs], [.. _outputs]);
        }

        public FlowEmulatorSnapshot Snapshot()
        {
            lock (_gate) return SnapshotCore();
        }

        internal IReadOnlyList<FlowVmInput> CaptureInputs()
        {
            lock (_gate)
            {
                ApplyPendingInputs();
                return _inputs.Values.OrderBy(input => input.PointId, StringComparer.Ordinal).ToArray();
            }
        }

        internal ulong Clock
        {
            get { lock (_gate) return _clock; }
        }

        internal void Publish(FlowVmScanResult scan)
        {
            lock (_gate) PublishCore(scan);
        }

        private void ScanCore()
        {
            ApplyPendingInputs();
            if (_fault == "communication_loss")
            {
                foreach (var pointId in _inputs.Keys.ToArray()) _inputs[pointId] = new FlowVmInput(pointId, false, false);
            }
            var scan = _machine.Scan(_inputs.Values.OrderBy(input => input.PointId, StringComparer.Ordinal).ToArray(), _clock);
            PublishCore(scan);
        }

        private void PublishCore(FlowVmScanResult scan)
        {
            _scanNumber = scan.ScanNumber;
            foreach (var command in scan.Commands)
            {
                var failed = _fault == "output_failure";
                _outputs.Add(new EmulatorOutputSample(
                    scan.ScanNumber, _clock, command.PointId, command.Value, !failed && command.Value,
                    failed ? "bad" : "good", "emulator", 16, null));
            }
            if (_outputs.Count > MaximumHistory) _outputs.RemoveRange(0, _outputs.Count - MaximumHistory);
        }

        private void ApplyPendingInputs()
        {
            var ready = _pending.TakeWhile(change => change.EffectiveAtMilliseconds <= _clock).ToArray();
            foreach (var change in ready) _inputs[change.PointId] = new FlowVmInput(change.PointId, change.Value, change.IsGood);
            _pending.RemoveRange(0, ready.Length);
        }

        private void ResetCore()
        {
            _machine.Reset();
            _scanNumber = 0;
            _outputs.Clear();
        }

        private FlowEmulatorSnapshot SnapshotCore() => new()
        {
            EmulatorId = Id,
            FlowId = _source.Id,
            ControllerTemplateId = _source.ControllerTemplateId,
            LifecycleState = _fault is null ? "ready" : "fault-injected",
            VirtualTimeMilliseconds = _clock,
            ScanNumber = _scanNumber,
            Inputs = _inputs.Values.OrderBy(input => input.PointId, StringComparer.Ordinal).ToArray(),
            OutputHistory = [.. _outputs],
            ActiveFault = _fault
        };

        private static IEnumerable<string> InputPointIds(ExecutableFlowSource source) => source.Nodes
            .Where(node => node.Kind == "digitalInput" && node.Configuration.TryGetValue("pointId", out _))
            .Select(node => node.Configuration["pointId"].GetString())
            .Where(static id => !string.IsNullOrEmpty(id))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal);

        public void Dispose() => _machine.Dispose();
    }
}
