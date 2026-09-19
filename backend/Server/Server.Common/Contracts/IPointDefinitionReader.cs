namespace Server.Common.Contracts;

/// <summary>Reads point definitions projected from point-source aggregates.</summary>
public interface IPointDefinitionReader
{
    /// <summary>Lists all points nested in persisted point-source aggregates.</summary>
    Task<IReadOnlyList<AutomationPoint>> ListPointsAsync(CancellationToken cancellationToken);

    /// <summary>Gets a point definition by its globally unique source/point identity.</summary>
    Task<AutomationPoint> GetPointAsync(
        string sourceId,
        string pointId,
        CancellationToken cancellationToken);
}