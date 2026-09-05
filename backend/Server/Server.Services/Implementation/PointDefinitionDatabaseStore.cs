using Server.Common.Contracts;
using Server.Common.Models;
using Server.Data.Context;
using Server.Data.Entities;
using System.Globalization;
using System.Text.Json;

namespace Server.Services.Implementation;

internal sealed class PointDefinitionDatabaseStore(
    IFlowControlDbContext context,
    TimeProvider timeProvider,
    IPointDefinitionValidator validator) : IPointDefinitionStore
{
    public async Task<IReadOnlyList<VirtualAutomationPoint>> ListPointsAsync(
        CancellationToken cancellationToken) =>
        [.. (await context.Points.AsNoTracking().ToListAsync(cancellationToken))
        .Select(DeserializePoint)
        .OrderBy(point => point.Name, StringComparer.OrdinalIgnoreCase)
        .ThenBy(point => point.Id, StringComparer.Ordinal)];

    public async Task<VirtualAutomationPoint> GetPointAsync(
        string id,
        CancellationToken cancellationToken)
    {
        var entity = await context.Points
            .AsNoTracking()
            .SingleOrDefaultAsync(point => point.Id == id, cancellationToken);
        return entity is null
            ? throw new PointDefinitionNotFoundException("point", id)
            : DeserializePoint(entity);
    }

    public async Task<VirtualAutomationPoint> CreatePointAsync(
        VirtualAutomationPoint point,
        CancellationToken cancellationToken)
    {
        validator.Validate(point, await Context(cancellationToken));
        var now = timeProvider.GetUtcNow();
        var created = point with
        {
            Revision = 1,
            CreatedAt = Timestamp(now),
            UpdatedAt = Timestamp(now)
        };
        context.Points.Add(Entity(created, now));
        await SaveCreate("point ID or name already exists", cancellationToken);
        return created;
    }

    public async Task<VirtualAutomationPoint> UpdatePointAsync(
        string id,
        VirtualAutomationPoint point,
        int revision,
        CancellationToken cancellationToken)
    {
        var entity = await FindPoint(id, cancellationToken);
        var previous = DeserializePoint(entity);
        EnsureRevision(revision, previous.Revision);
        validator.Validate(point, await Context(cancellationToken));
        var now = timeProvider.GetUtcNow();
        var updated = point with
        {
            Revision = previous.Revision + 1,
            CreatedAt = previous.CreatedAt,
            UpdatedAt = Timestamp(now)
        };
        if (point.Id == id)
        {
            Update(entity, updated, now);
            await SaveUpdate(entity, "point name already exists", cancellationToken);
        }
        else
        {
            // EF Core does not allow a tracked primary key to be changed. Replace
            // the row in one SaveChanges call so a rename is atomic while the
            // public resource revision and creation timestamp remain continuous.
            context.Points.Remove(entity);
            context.Points.Add(Entity(updated, now));
            await SaveUpdate(entity: null, "point ID or name already exists", cancellationToken);
        }
        return updated;
    }

    public async Task DeletePointAsync(
        string id,
        int revision,
        CancellationToken cancellationToken)
    {
        var entity = await FindPoint(id, cancellationToken);
        EnsureRevision(revision, DeserializePoint(entity).Revision);
        context.Points.Remove(entity);
        await SaveUpdate(entity: null, "unable to delete point", cancellationToken);
    }

    public async Task<IReadOnlyList<PointGroup>> ListGroupsAsync(
        CancellationToken cancellationToken) =>
        [.. (await context.PointGroups.AsNoTracking().ToListAsync(cancellationToken))
        .Select(DeserializeGroup)
        .OrderBy(group => group.Name, StringComparer.OrdinalIgnoreCase)
        .ThenBy(group => group.Id, StringComparer.Ordinal)];

    public async Task<PointGroup> GetGroupAsync(
        string id,
        CancellationToken cancellationToken)
    {
        var entity = await context.PointGroups
            .AsNoTracking()
            .SingleOrDefaultAsync(group => group.Id == id, cancellationToken);
        return entity is null
            ? throw new PointDefinitionNotFoundException("point group", id)
            : DeserializeGroup(entity);
    }

    public async Task<PointGroup> CreateGroupAsync(
        PointGroup group,
        CancellationToken cancellationToken)
    {
        validator.ValidateGroup(group, await Sources(cancellationToken));
        var now = timeProvider.GetUtcNow();
        var created = group with
        {
            Revision = 1,
            CreatedAt = Timestamp(now),
            UpdatedAt = Timestamp(now)
        };
        context.PointGroups.Add(Entity(created, now));
        await SaveCreate("group ID or name already exists", cancellationToken);
        return created;
    }

    public async Task<PointGroup> UpdateGroupAsync(
        string id,
        PointGroup group,
        int revision,
        CancellationToken cancellationToken)
    {
        var entity = await FindGroup(id, cancellationToken);
        var previous = DeserializeGroup(entity);
        if (group.Id != id)
        {
            throw new PointDefinitionValidationException(
                "group id must match request path");
        }
        EnsureRevision(revision, previous.Revision);
        validator.ValidateGroup(group, await Sources(cancellationToken));

        var members = await context.Points.AsNoTracking().ToListAsync(cancellationToken);
        var proposedGroups = (await ListGroupsAsync(cancellationToken))
            .Where(item => item.Id != id)
            .Append(group)
            .ToDictionary(item => item.Id, StringComparer.Ordinal);
        var validationContext = new PointValidationContext(
            proposedGroups,
            await Sources(cancellationToken));
        foreach (var member in members
            .Select(DeserializePoint)
            .Where(point => point.GroupId == id))
        {
            validator.Validate(member, validationContext);
        }

        var now = timeProvider.GetUtcNow();
        var updated = group with
        {
            Revision = previous.Revision + 1,
            CreatedAt = previous.CreatedAt,
            UpdatedAt = Timestamp(now)
        };
        Update(entity, updated, now);
        await SaveUpdate(entity, "group name already exists", cancellationToken);
        return updated;
    }

    public async Task DeleteGroupAsync(
        string id,
        int revision,
        CancellationToken cancellationToken)
    {
        var entity = await FindGroup(id, cancellationToken);
        EnsureRevision(revision, DeserializeGroup(entity).Revision);
        if ((await context.Points.AsNoTracking().ToListAsync(cancellationToken))
            .Select(DeserializePoint)
            .Any(point => point.GroupId == id))
        {
            throw new PointDefinitionConflictException(
                "group is referenced by one or more points");
        }

        context.PointGroups.Remove(entity);
        await SaveUpdate(entity: null, "unable to delete group", cancellationToken);
    }

    public async Task<IReadOnlyList<VirtualAutomationPoint>> MakePointsStandaloneAsync(
        string groupId,
        int groupRevision,
        CancellationToken cancellationToken)
    {
        var groupEntity = await FindGroup(groupId, cancellationToken);
        var group = DeserializeGroup(groupEntity);
        EnsureRevision(groupRevision, group.Revision);
        var entities = await context.Points.ToListAsync(cancellationToken);
        var sources = await Sources(cancellationToken);
        var groups = (await ListGroupsAsync(cancellationToken))
            .ToDictionary(item => item.Id, StringComparer.Ordinal);
        var validationContext = new PointValidationContext(groups, sources);
        var now = timeProvider.GetUtcNow();
        var updates = new List<(PointEntity Entity, VirtualAutomationPoint Point)>();
        foreach (var entity in entities)
        {
            var point = DeserializePoint(entity);
            if (point.GroupId != groupId)
            {
                continue;
            }

            var standalone = point with
            {
                GroupId = null,
                SourceId = point.SourceId ?? group.SourceId,
                Revision = point.Revision + 1,
                UpdatedAt = Timestamp(now)
            };
            validator.Validate(standalone, validationContext);
            updates.Add((entity, standalone));
        }

        foreach (var (entity, point) in updates)
        {
            Update(entity, point, now);
        }

        await SaveUpdate(entity: null, "unable to make points standalone", cancellationToken);
        return [.. updates
            .Select(update => update.Point)
            .OrderBy(point => point.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(point => point.Id, StringComparer.Ordinal)];
    }

    private async Task<PointValidationContext> Context(
        CancellationToken cancellationToken) =>
        new(
            (await ListGroupsAsync(cancellationToken))
                .ToDictionary(group => group.Id, StringComparer.Ordinal),
            await Sources(cancellationToken));

    private async Task<IReadOnlyDictionary<string, PointSource>> Sources(
        CancellationToken cancellationToken) =>
        (await context.PointSources.AsNoTracking().ToListAsync(cancellationToken))
        .Select(DeserializeSource)
        .ToDictionary(source => source.Id, StringComparer.Ordinal);

    private async Task<PointEntity> FindPoint(
        string id,
        CancellationToken cancellationToken) =>
        await context.Points.SingleOrDefaultAsync(point => point.Id == id, cancellationToken)
        ?? throw new PointDefinitionNotFoundException("point", id);

    private async Task<PointGroupEntity> FindGroup(
        string id,
        CancellationToken cancellationToken) =>
        await context.PointGroups.SingleOrDefaultAsync(
            group => group.Id == id,
            cancellationToken)
        ?? throw new PointDefinitionNotFoundException("point group", id);

    private async Task SaveCreate(
        string conflictMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraint(exception))
        {
            throw new PointDefinitionConflictException(conflictMessage, exception);
        }
    }

    private async Task SaveUpdate(
        BaseEntity? entity,
        string conflictMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            if (entity is not null)
            {
                await context.ReloadAsync(entity, cancellationToken);
            }
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new PointDefinitionConflictException("stale revision", exception);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraint(exception))
        {
            throw new PointDefinitionConflictException(conflictMessage, exception);
        }
    }

    private static void EnsureRevision(int supplied, int current)
    {
        if (supplied != current)
        {
            throw new PointDefinitionConflictException("stale revision");
        }
    }

    private static PointEntity Entity(VirtualAutomationPoint point, DateTimeOffset now) => new()
    {
        Id = point.Id,
        Key = NormalizeName(point.Name),
        Json = JsonSerializer.Serialize(point, FlowControlJson.Options),
        Created = now,
        Updated = now
    };

    private static PointGroupEntity Entity(PointGroup group, DateTimeOffset now) => new()
    {
        Id = group.Id,
        Key = NormalizeName(group.Name),
        Json = JsonSerializer.Serialize(group, FlowControlJson.Options),
        Created = now,
        Updated = now
    };

    private static void Update(PointEntity entity, VirtualAutomationPoint point, DateTimeOffset now)
    {
        entity.Key = NormalizeName(point.Name);
        entity.Json = JsonSerializer.Serialize(point, FlowControlJson.Options);
        entity.Updated = now;
    }

    private static void Update(
        PointGroupEntity entity,
        PointGroup group,
        DateTimeOffset now)
    {
        entity.Key = NormalizeName(group.Name);
        entity.Json = JsonSerializer.Serialize(group, FlowControlJson.Options);
        entity.Updated = now;
    }

    private static VirtualAutomationPoint DeserializePoint(PointEntity entity) =>
        JsonSerializer.Deserialize<VirtualAutomationPoint>(entity.Json, FlowControlJson.Options)
        ?? throw new InvalidOperationException($"Stored point {entity.Id} is null.");

    private static PointGroup DeserializeGroup(PointGroupEntity entity) =>
        JsonSerializer.Deserialize<PointGroup>(entity.Json, FlowControlJson.Options)
        ?? throw new InvalidOperationException($"Stored point group {entity.Id} is null.");

    private static PointSource DeserializeSource(PointSourceEntity entity) =>
        JsonSerializer.Deserialize<PointSource>(entity.Json, FlowControlJson.Options)
        ?? throw new InvalidOperationException($"Stored point source {entity.Id} is null.");

    private static string Timestamp(DateTimeOffset value) =>
        value.ToString(
            "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK",
            CultureInfo.InvariantCulture);

    private static string NormalizeName(string name) => name.ToUpperInvariant();

    private static bool IsUniqueConstraint(DbUpdateException exception) =>
        exception.InnerException?.Message.Contains(
            "UNIQUE constraint failed",
            StringComparison.Ordinal) == true;
}