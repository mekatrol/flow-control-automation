using Server.Common.Contracts;
using Server.Services.Contracts;
using System.Globalization;

namespace Server.Services.Implementation;

public sealed class VirtualPointRuntimeStore(TimeProvider timeProvider) : IVirtualPointRuntimeStore
{
    private readonly ReaderWriterLockSlim _gate = new(LockRecursionPolicy.NoRecursion);
    private readonly Dictionary<(string InstanceId, string PointKey), Cell> _cells = [];

    public Task ActivateFlowAsync(
        string executionInstanceId,
        string flowId,
        IReadOnlyList<VirtualPointDeclaration> declarations,
        IReadOnlySet<string> writerKeys,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var merged = ExecutionConfigurationService.MergeContracts(declarations);
        _gate.EnterWriteLock();
        try
        {
            foreach (var declaration in merged)
            {
                var identity = (executionInstanceId, declaration.Key);
                if (_cells.TryGetValue(identity, out var existing))
                {
                    if (!Compatible(existing.Contract, declaration))
                        throw new ExecutionConfigurationException($"virtual point '{declaration.Key}' conflicts with the instance-global contract", 409);
                    if (writerKeys.Contains(declaration.Key) && existing.WriterFlowId is not null && existing.WriterFlowId != flowId)
                        throw new VirtualPointWriterConflictException(executionInstanceId, declaration.Key, existing.WriterFlowId);
                }
            }

            foreach (var cell in _cells.Values.Where(item => item.ExecutionInstanceId == executionInstanceId && item.WriterFlowId == flowId && !writerKeys.Contains(item.PointKey)))
                cell.WriterFlowId = null;
            foreach (var declaration in merged)
            {
                var identity = (executionInstanceId, declaration.Key);
                if (!_cells.TryGetValue(identity, out var cell))
                {
                    cell = new Cell(executionInstanceId, declaration);
                    _cells.Add(identity, cell);
                }
                cell.Readers.Add(flowId);
                if (writerKeys.Contains(declaration.Key)) cell.WriterFlowId = flowId;
            }
        }
        finally { _gate.ExitWriteLock(); }
        return Task.CompletedTask;
    }

    public void ReleaseFlow(string executionInstanceId, string flowId)
    {
        _gate.EnterWriteLock();
        try
        {
            foreach (var cell in _cells.Values.Where(item => item.ExecutionInstanceId == executionInstanceId))
            {
                cell.Readers.Remove(flowId);
                if (cell.WriterFlowId == flowId) cell.WriterFlowId = null;
            }
        }
        finally { _gate.ExitWriteLock(); }
    }

    public bool TrySnapshot(string executionInstanceId, string pointKey, out VirtualPointRuntimeValue value)
    {
        _gate.EnterReadLock();
        try
        {
            if (_cells.TryGetValue((executionInstanceId, pointKey), out var cell))
            {
                value = Snapshot(cell);
                return true;
            }
            value = null!;
            return false;
        }
        finally { _gate.ExitReadLock(); }
    }

    public Task CommitAsync(string executionInstanceId, string flowId, IReadOnlyList<FlowVmCommand> commands, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var proposed = commands.Where(item => !item.IsInterface).ToList();
        _gate.EnterWriteLock();
        try
        {
            foreach (var command in proposed)
            {
                if (!_cells.TryGetValue((executionInstanceId, command.PointId), out var cell)) continue;
                if (cell.WriterFlowId != flowId)
                    throw new VirtualPointWriterConflictException(executionInstanceId, command.PointId, cell.WriterFlowId ?? "none");
                var expected = cell.Contract.ValueType == FlowPointValueType.Analog ? DataType.Number : DataType.Boolean;
                if (command.TypedValue.DataType != expected)
                    throw new InvalidOperationException($"Command for virtual point '{command.PointId}' has the wrong value type.");
            }

            var timestamp = timeProvider.GetUtcNow().ToString("O", CultureInfo.InvariantCulture);
            foreach (var command in proposed)
            {
                if (!_cells.TryGetValue((executionInstanceId, command.PointId), out var cell)) continue;
                cell.Value = command.TypedValue;
                cell.Timestamp = timestamp;
                cell.Version++;
            }
        }
        finally { _gate.ExitWriteLock(); }
        return Task.CompletedTask;
    }

    public IReadOnlyList<VirtualPointRuntimeValue> List(string executionInstanceId)
    {
        _gate.EnterReadLock();
        try { return _cells.Values.Where(item => item.ExecutionInstanceId == executionInstanceId).OrderBy(item => item.PointKey).Select(Snapshot).ToList(); }
        finally { _gate.ExitReadLock(); }
    }

    private static VirtualPointRuntimeValue Snapshot(Cell cell) => new()
    {
        ExecutionInstanceId = cell.ExecutionInstanceId,
        PointKey = cell.PointKey,
        Contract = cell.Contract,
        Value = cell.Value ?? Default(cell.Contract),
        Quality = cell.Value is not null || cell.Contract.RelinquishDefault is not null ? DataQuality.Good : DataQuality.Unavailable,
        Timestamp = cell.Timestamp,
        WriterFlowId = cell.WriterFlowId,
        Version = cell.Version
    };

    private static FlowVmValue? Default(VirtualPointDeclaration contract) => contract.RelinquishDefault is not { } value ? null :
        contract.ValueType == FlowPointValueType.Analog && value.TryGetDouble(out var number)
            ? FlowVmValue.FromNumber(number)
            : value.ValueKind is System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False
                ? FlowVmValue.FromBoolean(value.GetBoolean()) : null;

    private static bool Compatible(VirtualPointDeclaration left, VirtualPointDeclaration right) =>
        left.ValueType == right.ValueType && left.Units == right.Units && left.Persistence == right.Persistence
        && System.Text.Json.JsonSerializer.Serialize(left.RelinquishDefault) == System.Text.Json.JsonSerializer.Serialize(right.RelinquishDefault);

    private sealed class Cell(string executionInstanceId, VirtualPointDeclaration contract)
    {
        public string ExecutionInstanceId { get; } = executionInstanceId;
        public string PointKey { get; } = contract.Key;
        public VirtualPointDeclaration Contract { get; } = contract;
        public HashSet<string> Readers { get; } = new(StringComparer.Ordinal);
        public string? WriterFlowId { get; set; }
        public FlowVmValue? Value { get; set; }
        public string? Timestamp { get; set; }
        public ulong Version { get; set; }
    }
}
