namespace Server.Common.Contracts.Points;

public interface IPointMappingExecutionService
{
    Task<PointMappingReadResult> ReadAsync(PointMappingResolution resolution, CancellationToken cancellationToken);
    Task<PointMappingCommandResult> CommandAsync(PointMappingResolution resolution, object? value, CancellationToken cancellationToken);
}