using Server.Services.Contracts;

namespace Server.Services;

public interface IPointDefinitionStore
{
    Task<IReadOnlyList<Point>> ListPointsAsync(CancellationToken cancellationToken);

    Task<Point> GetPointAsync(string id, CancellationToken cancellationToken);

    Task<Point> CreatePointAsync(Point point, CancellationToken cancellationToken);

    Task<Point> UpdatePointAsync(
        string id,
        Point point,
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

    Task<IReadOnlyList<Point>> MakePointsStandaloneAsync(
        string groupId,
        int groupRevision,
        CancellationToken cancellationToken);
}