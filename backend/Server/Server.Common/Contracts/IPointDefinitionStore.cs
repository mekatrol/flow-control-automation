using Server.Common.Models;

namespace Server.Common.Contracts;

public interface IPointDefinitionStore
{
    Task<IReadOnlyList<VirtualAutomationPoint>> ListPointsAsync(CancellationToken cancellationToken);

    Task<VirtualAutomationPoint> GetPointAsync(string id, CancellationToken cancellationToken);

    Task<VirtualAutomationPoint> CreatePointAsync(VirtualAutomationPoint point, CancellationToken cancellationToken);

    Task<VirtualAutomationPoint> UpdatePointAsync(
        string id,
        VirtualAutomationPoint point,
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

    Task<IReadOnlyList<VirtualAutomationPoint>> MakePointsStandaloneAsync(
        string groupId,
        int groupRevision,
        CancellationToken cancellationToken);
}