using Server.Common.Contracts;
using System.Text.Json;

namespace Server.Services.Implementation;

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
        var reader = scope.ServiceProvider.GetRequiredService<IPointReadService>();
        var result = new FlowVmInput[pointIds.Count];
        for (var index = 0; index < pointIds.Count; index++)
        {
            if (virtualPoints.TrySnapshot("server", pointIds[index], out var snapshot))
            {
                result[index] = new FlowVmInput(
                    pointIds[index],
                    snapshot.Value ?? FlowVmValue.FromBoolean(false, DataQuality.Unavailable));
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
        commands = [.. commands.Where(command => !command.IsInterface)];
        cancellationToken.ThrowIfCancellationRequested();
        await virtualPoints.CommitAsync("server", flowId, commands, cancellationToken);
        lock (_gate)
        {
            _latestCommands[flowId] = [.. commands];
        }
    }

    private static FlowVmValue ParseValue(string? json, DataQuality quality)
    {
        if (json is null)
        {
            return FlowVmValue.FromBoolean(false, DataQuality.Bad);
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
                _ => FlowVmValue.FromBoolean(false, DataQuality.Bad)
            };
        }
        catch (JsonException)
        {
            return FlowVmValue.FromBoolean(false, DataQuality.Bad);
        }
    }
}