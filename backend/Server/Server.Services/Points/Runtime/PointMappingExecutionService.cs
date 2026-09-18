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

    public Task<PointMappingReadResult> ReadMappingAsync(PointSource source, PointMapping mapping, CancellationToken cancellationToken) =>
        Adapter(source).ReadMappingAsync(source, mapping, cancellationToken);

    public Task<PointMappingCommandResult> CommandMappingAsync(
        PointSource source, PointMapping mapping, string payload, CancellationToken cancellationToken) =>
        Adapter(source).CommandMappingAsync(source, mapping, payload, cancellationToken);

    private IPointMappingAdapter Adapter(PointMappingResolution resolution) =>
        Adapter(resolution.Source);

    private IPointMappingAdapter Adapter(PointSource source) =>
        _adapters.TryGetValue(source.Kind, out var adapter)
            ? adapter
            : throw new InvalidOperationException($"No mapping adapter is registered for '{source.Kind}'.");
}