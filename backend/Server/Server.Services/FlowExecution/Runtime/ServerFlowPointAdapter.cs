using System.Text.Json;

namespace Server.Services.FlowExecution.Runtime;

internal sealed class ServerFlowPointAdapter(
    IServiceScopeFactory scopes,
    IVirtualPointRuntimeStore virtualPoints) : IFlowPointAdapter
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, IReadOnlyList<FlowVmCommand>> _latestCommands = new(StringComparer.Ordinal);

    public async Task<IReadOnlyList<FlowVmInput>> ReadAsync(
        IReadOnlyList<string> pointIds,
        CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IPointValueReader>();
        var result = new FlowVmInput[pointIds.Count];

        for (var index = 0; index < pointIds.Count; index++)
        {
            if (virtualPoints.TrySnapshot("server", pointIds[index], out var snapshot))
            {
                result[index] = new FlowVmInput(
                    pointIds[index],
                    snapshot.Value ?? FlowVmValue.FromBoolean(false, DataQualityType.Unavailable));
                continue;
            }

            var envelope = await reader.ReadAsync(pointIds[index], cancellationToken);
            result[index] = new FlowVmInput(pointIds[index], ParseValue(envelope.Value?.ToJsonString(), envelope.Quality));
        }

        return result;
    }

    public async Task PublishAsync(
        string flowId,
        IReadOnlyList<FlowVmCommand> commands,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await virtualPoints.CommitAsync("server", flowId, commands, cancellationToken);

        await using var scope = scopes.CreateAsyncScope();
        var definitions = scope.ServiceProvider.GetRequiredService<IPointDefinitionReader>();
        var sources = scope.ServiceProvider.GetRequiredService<IPointSourceService>();
        var resolver = scope.ServiceProvider.GetRequiredService<IPointMappingResolver>();
        var mappings = scope.ServiceProvider.GetRequiredService<IPointMappingExecutionService>();

        foreach (var command in commands)
        {
            var separator = command.PointId.IndexOf('/', StringComparison.Ordinal);

            if (separator <= 0 || separator == command.PointId.Length - 1)
            {
                continue;
            }

            var sourceId = command.PointId[..separator];
            var pointId = command.PointId[(separator + 1)..];
            var point = await definitions.GetPointAsync(sourceId, pointId, cancellationToken);

            if (!point.Enabled || !point.Commandable)
            {
                throw new InvalidOperationException($"Point '{command.PointId}' is not enabled and commandable.");
            }

            var source = await sources.GetAsync(sourceId, cancellationToken);

            if (!source.Enabled)
            {
                throw new InvalidOperationException($"Point source '{sourceId}' is disabled.");
            }

            if (source.Kind == PointSourceKind.Virtual || point.Direction == DataDirectionType.Value)
            {
                continue;
            }

            var result = await mappings.CommandAsync(
                resolver.Resolve(source, point),
                command.TypedValue.DataType == DataType.Number
                    ? command.TypedValue.Number
                    : command.TypedValue.Boolean,
                cancellationToken);

            if (result.Diagnostic is not null)
            {
                throw new InvalidOperationException(
                    $"Commanding point '{command.PointId}' failed: {result.Diagnostic}");
            }
        }

        lock (_gate)
        {
            _latestCommands[flowId] = [.. commands];
        }
    }

    private static FlowVmValue ParseValue(string? json, DataQualityType quality)
    {
        if (json is null)
        {
            return FlowVmValue.FromBoolean(false, DataQualityType.Bad);
        }

        try
        {
            using var document = JsonDocument.Parse(json);

            return document.RootElement.ValueKind switch
            {
                JsonValueKind.True => FlowVmValue.FromBoolean(true, quality),
                JsonValueKind.False => FlowVmValue.FromBoolean(false, quality),
                JsonValueKind.Number when document.RootElement.TryGetDouble(out var number) && double.IsFinite(number) =>
                    FlowVmValue.FromNumber(number, quality),
                _ => FlowVmValue.FromBoolean(false, DataQualityType.Bad)
            };
        }
        catch (JsonException)
        {
            return FlowVmValue.FromBoolean(false, DataQualityType.Bad);
        }
    }
}