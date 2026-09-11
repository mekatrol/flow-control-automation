namespace Server.Common.Contracts;

public interface IPointDefinitionStore
{
    Task<IReadOnlyList<AutomationPoint>> ListPointsAsync(CancellationToken cancellationToken);

    Task<AutomationPoint> GetPointAsync(string id, CancellationToken cancellationToken);

    Task<AutomationPoint> CreatePointAsync(AutomationPoint point, CancellationToken cancellationToken);

    Task<AutomationPoint> UpdatePointAsync(
        string id,
        AutomationPoint point,
        int revision,
        CancellationToken cancellationToken);

    Task DeletePointAsync(string id, int revision, CancellationToken cancellationToken);

}
