using Server.Common.Models;

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

    Task<IReadOnlyList<PointGroup>> ListGroupsAsync(CancellationToken cancellationToken);

    Task<PointGroup> GetGroupAsync(string id, CancellationToken cancellationToken);

    Task<PointGroup> CreateGroupAsync(
        PointGroup group,
        CancellationToken cancellationToken);

    Task<PointGroup> UpdateGroupAsync(
        string id,
        PointGroup group,
        int revision,
        CancellationToken cancellationToken);

    Task DeleteGroupAsync(string id, int revision, CancellationToken cancellationToken);

    Task<IReadOnlyList<AutomationPoint>> MakePointsStandaloneAsync(
        string groupId,
        int groupRevision,
        CancellationToken cancellationToken);
}