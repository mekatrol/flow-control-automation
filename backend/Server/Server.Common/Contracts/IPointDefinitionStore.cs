namespace Server.Common.Contracts;

public interface IPointDefinitionStore
{
    Task<IReadOnlyList<AutomationPoint>> ListPointsAsync(CancellationToken cancellationToken);

    Task<AutomationPoint> GetPointAsync(string id, CancellationToken cancellationToken);

    [Obsolete("Points are written only through their owning point-source aggregate.")]
    Task<AutomationPoint> CreatePointAsync(AutomationPoint point, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Points are written only through their owning point-source aggregate.");

    [Obsolete("Points are written only through their owning point-source aggregate.")]
    Task<AutomationPoint> UpdatePointAsync(string id, AutomationPoint point, int revision, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Points are written only through their owning point-source aggregate.");

    [Obsolete("Points are deleted only with their owning point-source aggregate.")]
    Task DeletePointAsync(string id, int revision, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Points are deleted only with their owning point-source aggregate.");
}
