namespace Server.Services.Points.Runtime;

internal sealed class PointMappingExecutionService(IEnumerable<IPointMappingAdapter> adapters)
    : IPointMappingExecutionService
{
    private readonly Dictionary<PointSourceKind, IPointMappingAdapter> _adapters = adapters
        .ToDictionary(adapter => adapter.Kind);

    public Task<PointMappingReadResult> ReadAsync(PointMappingResolution resolution, CancellationToken cancellationToken) =>
        Adapter(resolution).ReadAsync(resolution, cancellationToken);

    public Task<PointMappingCommandResult> CommandAsync(PointMappingResolution resolution, object? value, CancellationToken cancellationToken) =>
        Adapter(resolution).CommandAsync(resolution, value, cancellationToken);

    private IPointMappingAdapter Adapter(PointMappingResolution resolution) =>
        _adapters.TryGetValue(resolution.Source.Kind, out var adapter)
            ? adapter
            : throw new InvalidOperationException($"No mapping adapter is registered for '{resolution.Source.Kind}'.");
}