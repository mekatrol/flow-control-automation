namespace Server.Common.Contracts;

/// <summary>Reads point definitions projected from point-source aggregates.</summary>
public interface IPointDefinitionReader
{
    /// <summary>Lists all points nested in persisted point-source aggregates.</summary>
    Task<IReadOnlyList<AutomationPoint>> ListPointsAsync(CancellationToken cancellationToken);

    /// <summary>Gets a globally identified point definition from its owning source aggregate.</summary>
    Task<AutomationPoint> GetPointAsync(string id, CancellationToken cancellationToken);
}