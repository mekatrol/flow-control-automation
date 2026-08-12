using Microsoft.Extensions.DependencyInjection;
using Server.Services.Contracts;
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
            var value = false;
            var isGood = string.Equals(envelope.Quality, "good", StringComparison.Ordinal)
                && envelope.Value is not null
                && TryBoolean(envelope.Value.ToJsonString(), out value);
            result[index] = new FlowVmInput(pointIds[index], isGood && value, isGood);
        }

        return result;
    }

    public Task PublishAsync(
        string flowId,
        IReadOnlyList<FlowVmCommand> commands,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _latestCommands[flowId] = commands.ToArray();
        }

        return Task.CompletedTask;
    }

    private static bool TryBoolean(string json, out bool value)
    {
        try
        {
            value = JsonSerializer.Deserialize<bool>(json);
            return true;
        }
        catch (JsonException)
        {
            value = false;
            return false;
        }
    }
}
