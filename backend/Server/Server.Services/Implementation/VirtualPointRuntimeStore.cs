using Server.Common.Contracts;
using Server.Common.Models;
using System.Globalization;

namespace Server.Services.Implementation;

public sealed class VirtualPointRuntimeStore(
    TimeProvider timeProvider,
    IVirtualPointRetainedStore? retainedStore = null) : IVirtualPointRuntimeStore
{
    private readonly ReaderWriterLockSlim _gate = new(LockRecursionPolicy.NoRecursion);
    private readonly SemaphoreSlim _commitGate = new(1, 1);
    private readonly Dictionary<(string InstanceId, string PointKey), Cell> _cells = [];

    public async Task ActivateFlowAsync(
        string executionInstanceId,
        string flowId,
        IReadOnlyList<VirtualPointDeclaration> declarations,
        IReadOnlySet<string> writerKeys,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var merged = ExecutionConfigurationService.MergeContracts(declarations);
        var retained = new Dictionary<string, RetainedVirtualPointValue?>(StringComparer.Ordinal);
        if (retainedStore is not null)
        {
            foreach (var declaration in merged.Where(item => item.Persistence == VirtualPointPersistence.Retained))
            {
                retained[declaration.Key] = await retainedStore.ReadAsync(executionInstanceId, declaration.Key, cancellationToken);
            }
        }
        _gate.EnterWriteLock();
        try
        {
            var existingKeys = _cells.Keys
                .Where(identity => identity.InstanceId == executionInstanceId)
                .Select(identity => identity.PointKey)
                .ToHashSet(StringComparer.Ordinal);
            var allocatedCount = existingKeys.Union(merged.Select(item => item.Key), StringComparer.Ordinal).Count();
            if (allocatedCount > ExecutionConfigurationService.MaximumVirtualPointsPerContext)
            {
                throw new ExecutionConfigurationException(
                    $"execution instance exceeds the {ExecutionConfigurationService.MaximumVirtualPointsPerContext} virtual-point limit",
                    422,
                    "virtual_point_limit_exceeded",
                    new { limit = ExecutionConfigurationService.MaximumVirtualPointsPerContext, actual = allocatedCount });
            }
            foreach (var declaration in merged)
            {
                var identity = (executionInstanceId, declaration.Key);
                if (_cells.TryGetValue(identity, out var existing))
                {
                    if (!Compatible(existing.Contract, declaration))
                    {
                        throw new ExecutionConfigurationException($"virtual point '{declaration.Key}' conflicts with the instance-global contract", 409);
                    }

                    if (writerKeys.Contains(declaration.Key) && existing.WriterFlowId is not null && existing.WriterFlowId != flowId)
                    {
                        throw new VirtualPointWriterConflictException(executionInstanceId, declaration.Key, existing.WriterFlowId);
                    }
                }
            }

            foreach (var cell in _cells.Values.Where(item => item.ExecutionInstanceId == executionInstanceId && item.WriterFlowId == flowId && !writerKeys.Contains(item.PointKey)))
            {
                cell.WriterFlowId = null;
            }

            foreach (var declaration in merged)
            {
                var identity = (executionInstanceId, declaration.Key);
                if (!_cells.TryGetValue(identity, out var cell))
                {
                    cell = new Cell(executionInstanceId, declaration);
                    if (retained.GetValueOrDefault(declaration.Key) is { } restored)
                    {
                        var expected = declaration.ValueType == FlowPointValueType.Analog ? DataType.Number : DataType.Boolean;
                        if (restored.Value.DataType == expected && Compatible(declaration, restored.Contract))
                        {
                            cell.Value = restored.Value;
                            cell.Timestamp = restored.Timestamp;
                            cell.Version = restored.Version;
                        }
                    }
                    _cells.Add(identity, cell);
                }
                cell.Readers.Add(flowId);
                if (writerKeys.Contains(declaration.Key))
                {
                    cell.WriterFlowId = flowId;
                }
            }
        }
        finally { _gate.ExitWriteLock(); }
    }

    public void ReleaseFlow(string executionInstanceId, string flowId)
    {
        _gate.EnterWriteLock();
        try
        {
            foreach (var cell in _cells.Values.Where(item => item.ExecutionInstanceId == executionInstanceId))
            {
                cell.Readers.Remove(flowId);
                if (cell.WriterFlowId == flowId)
                {
                    cell.WriterFlowId = null;
                }
            }
            foreach (var identity in _cells
                .Where(item => item.Key.InstanceId == executionInstanceId
                    && item.Value.Contract.Persistence == VirtualPointPersistence.Volatile
                    && item.Value.WriterFlowId is null
                    && item.Value.Readers.Count == 0)
                .Select(item => item.Key)
                .ToArray())
            {
                _cells.Remove(identity);
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

    public async Task CommitAsync(string executionInstanceId, string flowId, IReadOnlyList<FlowVmCommand> commands, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var proposed = commands.ToList();
        await _commitGate.WaitAsync(cancellationToken);
        try
        {
            Dictionary<string, RetainedVirtualPointValue> retainedWrites;
            _gate.EnterWriteLock();
            try
            {
                foreach (var command in proposed)
                {
                    if (!_cells.TryGetValue((executionInstanceId, command.PointId), out var cell))
                    {
                        continue;
                    }

                    if (cell.WriterFlowId != flowId)
                    {
                        throw new VirtualPointWriterConflictException(executionInstanceId, command.PointId, cell.WriterFlowId ?? "none");
                    }

                    var expected = cell.Contract.ValueType == FlowPointValueType.Analog ? DataType.Number : DataType.Boolean;
                    if (command.TypedValue.DataType != expected)
                    {
                        throw new InvalidOperationException($"Command for virtual point '{command.PointId}' has the wrong value type.");
                    }
                }

                var timestamp = timeProvider.GetUtcNow().ToString("O", CultureInfo.InvariantCulture);
                retainedWrites = proposed
                    .Where(command => _cells.TryGetValue((executionInstanceId, command.PointId), out var cell)
                        && cell.Contract.Persistence == VirtualPointPersistence.Retained)
                    .ToDictionary(
                        command => command.PointId,
                        command =>
                        {
                            var cell = _cells[(executionInstanceId, command.PointId)];
                            return new RetainedVirtualPointValue(command.TypedValue, timestamp, checked(cell.Version + 1), cell.Contract);
                        },
                        StringComparer.Ordinal);
                foreach (var command in proposed)
                {
                    if (!_cells.TryGetValue((executionInstanceId, command.PointId), out var cell))
                    {
                        continue;
                    }

                    cell.Value = command.TypedValue;
                    cell.Timestamp = timestamp;
                    cell.Version++;
                }
            }
            finally { _gate.ExitWriteLock(); }

            if (retainedStore is not null && retainedWrites.Count > 0)
            {
                await retainedStore.WriteAsync(executionInstanceId, retainedWrites, cancellationToken);
            }
        }
        finally { _commitGate.Release(); }
    }

    public IReadOnlyList<VirtualPointRuntimeValue> List(string executionInstanceId)
    {
        _gate.EnterReadLock();
        try { return [.. _cells.Values.Where(item => item.ExecutionInstanceId == executionInstanceId).OrderBy(item => item.PointKey).Select(Snapshot)]; }
        finally { _gate.ExitReadLock(); }
    }

    public async Task ClearRetainedAsync(string executionInstanceId, CancellationToken cancellationToken)
    {
        await _commitGate.WaitAsync(cancellationToken);
        try
        {
            if (retainedStore is not null)
            {
                await retainedStore.ClearAsync(executionInstanceId, cancellationToken);
            }

            _gate.EnterWriteLock();
            try
            {
                foreach (var cell in _cells.Values.Where(item => item.ExecutionInstanceId == executionInstanceId && item.Contract.Persistence == VirtualPointPersistence.Retained))
                {
                    cell.Value = null;
                    cell.Timestamp = null;
                    cell.Version++;
                }
            }
            finally { _gate.ExitWriteLock(); }
        }
        finally { _commitGate.Release(); }
    }

    public async Task RestoreRetainedAsync(string executionInstanceId, IReadOnlyDictionary<string, RetainedVirtualPointValue> values, CancellationToken cancellationToken)
    {
        await _commitGate.WaitAsync(cancellationToken);
        try
        {
            _gate.EnterWriteLock();
            try
            {
                foreach (var (pointKey, retained) in values)
                {
                    if (!_cells.TryGetValue((executionInstanceId, pointKey), out var cell)
                        || cell.Contract.Persistence != VirtualPointPersistence.Retained)
                    {
                        throw new ExecutionConfigurationException($"retained backup point '{pointKey}' is not allocated as retained", 422, "incompatible_retained_backup");
                    }

                    var expected = cell.Contract.ValueType == FlowPointValueType.Analog ? DataType.Number : DataType.Boolean;
                    if (retained.Value.DataType != expected || !Compatible(cell.Contract, retained.Contract))
                    {
                        throw new ExecutionConfigurationException($"retained backup point '{pointKey}' has the wrong type", 422, "incompatible_retained_backup");
                    }
                }
                foreach (var cell in _cells.Values.Where(item => item.ExecutionInstanceId == executionInstanceId && item.Contract.Persistence == VirtualPointPersistence.Retained))
                {
                    if (values.TryGetValue(cell.PointKey, out var retained))
                    {
                        cell.Value = retained.Value;
                        cell.Timestamp = retained.Timestamp;
                        cell.Version = retained.Version;
                    }
                    else { cell.Value = null; cell.Timestamp = null; cell.Version++; }
                }
            }
            finally { _gate.ExitWriteLock(); }
            if (retainedStore is not null)
            {
                await retainedStore.ReplaceAsync(executionInstanceId, values, cancellationToken);
            }
        }
        finally { _commitGate.Release(); }
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
        ReaderFlowIds = [.. cell.Readers.Order(StringComparer.Ordinal)],
        Retained = cell.Contract.Persistence == VirtualPointPersistence.Retained,
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