using Server.Common.Contracts;

namespace Server.Services;

public interface IPointDefinitionStore
{
    Task<IReadOnlyList<FlowPoint>> ListPointsAsync(CancellationToken cancellationToken);

    Task<FlowPoint> GetPointAsync(string id, CancellationToken cancellationToken);

    Task<FlowPoint> CreatePointAsync(FlowPoint point, CancellationToken cancellationToken);

    Task<FlowPoint> UpdatePointAsync(
        string id,
        FlowPoint point,
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

    Task<IReadOnlyList<FlowPoint>> MakePointsStandaloneAsync(
        string groupId,
        int groupRevision,
        CancellationToken cancellationToken);
}