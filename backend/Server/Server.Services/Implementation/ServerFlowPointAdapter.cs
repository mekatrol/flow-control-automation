using Server.Services.Contracts;
using Server.Services.Extensions;
using System.Text.Json;

namespace Server.Services.Implementation;

internal sealed class ServerFlowPointAdapter(IServiceScopeFactory scopes) : IFlowPointAdapter
{
    private readonly object _gate = new();
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
            var envelope = await reader.ReadAsync(pointIds[index], cancellationToken);
            var quality = string.Equals(envelope.Quality, DataQualityExtensions.Good, StringComparison.Ordinal) ? DataQualityExtensions.Good : DataQualityExtensions.Bad;
            result[index] = new FlowVmInput(pointIds[index], ParseValue(envelope.Value?.ToJsonString(), quality));
        }

        return result;
    }

    public Task PublishAsync(
        string flowId,
        IReadOnlyList<FlowVmCommand> commands,
        CancellationToken cancellationToken)
    {
        commands = [.. commands.Where(command => !command.IsInterface)];
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _latestCommands[flowId] = [.. commands];
        }

        return Task.CompletedTask;
    }

    private static FlowVmValue ParseValue(string? json, string quality)
    {
        if (json is null)
        {
            return FlowVmValue.FromBoolean(false, DataQualityExtensions.Bad);
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
                _ => FlowVmValue.FromBoolean(false, DataQualityExtensions.Bad)
            };
        }
        catch (JsonException)
        {
            return FlowVmValue.FromBoolean(false, DataQualityExtensions.Bad);
        }
    }
}