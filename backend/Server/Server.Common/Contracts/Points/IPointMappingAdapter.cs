namespace Server.Common.Contracts.Points;

public interface IPointMappingAdapter
{
    PointSourceKind Kind { get; }

    Task<PointMappingReadResult> ReadAsync(
        PointMappingResolution resolution,
        CancellationToken cancellationToken);

    Task<PointMappingCommandResult> CommandAsync(
        PointMappingResolution resolution,
        object? value,
        CancellationToken cancellationToken);
}