using Server.Data.Context;
using Server.Data.Entities;
using Server.Services.Contracts;
using System.Globalization;
using System.Text.Json;

namespace Server.Services.Implementation;

internal sealed class PointSourceDatabaseService(
    IFlowControlDbContext context,
    TimeProvider timeProvider,
    IPointSourceValidator validator) : IPointSourceService
{
    public async Task<PaginatedResult<PointSource>> ListAsync(
        PointSourceListOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Page < 1
            || options.PageSize is not (10 or 20 or 50)
            || options.SortDirection is not ("ascending" or "descending"))
        {
            throw new PointSourceValidationException("invalid pagination or sort query");
        }

        var filter = options.Filter.Trim();
        var items = (await context.PointSources
                .AsNoTracking()
                .ToListAsync(cancellationToken))
            .Select(Deserialize)
            .Where(source =>
                filter.Length == 0
                || source.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(
                source => source,
                options.SortDirection == "descending"
                    ? DescendingComparer.Instance
                    : AscendingComparer.Instance)
            .ToList();
        var pageCount = Math.Max(1, (items.Count + options.PageSize - 1) / options.PageSize);
        var page = Math.Clamp(options.Page, 1, pageCount);
        return new(
            [.. items.Skip((page - 1) * options.PageSize).Take(options.PageSize)],
            items.Count,
            page,
            options.PageSize,
            pageCount);
    }

    public async Task<PointSource> GetAsync(
        string id,
        CancellationToken cancellationToken)
    {
        var entity = await context.PointSources
            .AsNoTracking()
            .SingleOrDefaultAsync(source => source.Id == id, cancellationToken);
        return entity is null ? throw new PointSourceNotFoundException(id) : Deserialize(entity);
    }

    public async Task<PointSource> CreateAsync(
        PointSource source,
        CancellationToken cancellationToken)
    {
        validator.Validate(source);
        await EnsureNameAvailable(source.Name, exceptId: null, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var created = source with
        {
            Revision = 1,
            CreatedAt = Timestamp(now),
            UpdatedAt = Timestamp(now)
        };
        context.PointSources.Add(new PointSourceEntity
        {
            Id = created.Id,
            // The primary key already enforces unique IDs. Keeping the normalized
            // name in the separately indexed Key column makes name uniqueness
            // atomic even when two server processes create sources concurrently.
            Key = NormalizeName(created.Name),
            Json = Serialize(created),
            Created = now,
            Updated = now
        });
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraint(exception))
        {
            throw new PointSourceConflictException(
                "source ID or name already exists",
                exception);
        }

        return created;
    }

    public async Task<PointSource> UpdateAsync(
        string id,
        PointSource source,
        int revision,
        CancellationToken cancellationToken)
    {
        var entity = await FindTracked(id, cancellationToken);
        var previous = Deserialize(entity);
        if (source.Id != id)
        {
            throw new PointSourceValidationException("source id must match request path");
        }

        if (revision != previous.Revision)
        {
            throw new PointSourceConflictException("stale revision");
        }

        if (!string.Equals(source.Kind, previous.Kind, StringComparison.Ordinal)
            && await IsReferenced(id, cancellationToken))
        {
            throw new PointSourceConflictException(
                "source kind cannot change while points or groups reference it");
        }

        validator.Validate(source);
        await EnsureNameAvailable(source.Name, id, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var updated = source with
        {
            Revision = previous.Revision + 1,
            CreatedAt = previous.CreatedAt,
            UpdatedAt = Timestamp(now)
        };
        entity.Json = Serialize(updated);
        entity.Key = NormalizeName(updated.Name);
        entity.Updated = now;
        try
        {
            await SaveWithConcurrencyMapping(entity, cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraint(exception))
        {
            throw new PointSourceConflictException(
                "source name already exists",
                exception);
        }

        return updated;
    }

    public async Task DeleteAsync(
        string id,
        int revision,
        CancellationToken cancellationToken)
    {
        var entity = await FindTracked(id, cancellationToken);
        if (Deserialize(entity).Revision != revision)
        {
            throw new PointSourceConflictException("stale revision");
        }

        if (await IsReferenced(id, cancellationToken))
        {
            throw new PointSourceConflictException(
                "source is referenced by one or more points or groups");
        }

        context.PointSources.Remove(entity);
        await SaveWithConcurrencyMapping(entity: null, cancellationToken);
    }

    private async Task EnsureNameAvailable(
        string name,
        string? exceptId,
        CancellationToken cancellationToken)
    {
        var sources = await context.PointSources.AsNoTracking().ToListAsync(cancellationToken);
        if (sources.Any(entity =>
            entity.Id != exceptId
            && string.Equals(
                Deserialize(entity).Name,
                name,
                StringComparison.OrdinalIgnoreCase)))
        {
            throw new PointSourceConflictException("source name already exists");
        }
    }

    private async Task<PointSourceEntity> FindTracked(
        string id,
        CancellationToken cancellationToken) =>
        await context.PointSources.SingleOrDefaultAsync(
            source => source.Id == id,
            cancellationToken)
        ?? throw new PointSourceNotFoundException(id);

    private async Task<bool> IsReferenced(
        string id,
        CancellationToken cancellationToken)
    {
        var groups = await context.PointGroups.AsNoTracking().ToListAsync(cancellationToken);
        if (groups.Select(DeserializeGroup).Any(group => group.SourceId == id))
        {
            return true;
        }

        var points = await context.Points.AsNoTracking().ToListAsync(cancellationToken);
        return points.Select(DeserializePoint).Any(point => point.SourceId == id);
    }

    private async Task SaveWithConcurrencyMapping(
        PointSourceEntity? entity,
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
            throw new PointSourceConflictException("stale revision", exception);
        }
    }

    private static string Timestamp(DateTimeOffset value) =>
        value.ToString(
            "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK",
            CultureInfo.InvariantCulture);

    private static string Serialize(PointSource source) =>
        JsonSerializer.Serialize(source, FlowControlJson.Options);

    private static PointSource Deserialize(PointSourceEntity entity) =>
        JsonSerializer.Deserialize<PointSource>(entity.Json, FlowControlJson.Options)
        ?? throw new InvalidOperationException($"Stored point source {entity.Id} is null.");

    private static PointGroup DeserializeGroup(PointGroupEntity entity) =>
        JsonSerializer.Deserialize<PointGroup>(entity.Json, FlowControlJson.Options)
        ?? throw new InvalidOperationException($"Stored point group {entity.Id} is null.");

    private static Point DeserializePoint(PointEntity entity) =>
        JsonSerializer.Deserialize<Point>(entity.Json, FlowControlJson.Options)
        ?? throw new InvalidOperationException($"Stored point {entity.Id} is null.");

    private static bool IsUniqueConstraint(DbUpdateException exception) =>
        exception.InnerException?.Message.Contains(
            "UNIQUE constraint failed",
            StringComparison.Ordinal) == true;

    private static string NormalizeName(string name) =>
        name.ToUpperInvariant();

    private sealed class AscendingComparer : IComparer<PointSource>
    {
        public static AscendingComparer Instance { get; } = new();

        public int Compare(PointSource? left, PointSource? right)
        {
            var comparison = StringComparer.OrdinalIgnoreCase.Compare(left?.Name, right?.Name);
            return comparison != 0
                ? comparison
                : StringComparer.Ordinal.Compare(left?.Id, right?.Id);
        }
    }

    private sealed class DescendingComparer : IComparer<PointSource>
    {
        public static DescendingComparer Instance { get; } = new();

        public int Compare(PointSource? left, PointSource? right) =>
            -AscendingComparer.Instance.Compare(left, right);
    }
}