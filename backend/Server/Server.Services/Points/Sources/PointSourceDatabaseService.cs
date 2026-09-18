using Server.Data.Context;
using Server.Data.Entities;
using System.Globalization;
using System.Text.Json;

namespace Server.Services.Points.Sources;

internal sealed class PointSourceDatabaseService(
    IFlowControlDbContext context,
    TimeProvider timeProvider,
    IPointSourceValidator validator,
    IPointDefinitionValidator pointValidator) : IPointSourceService
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
        var existingSources = await LoadSources(cancellationToken);
        ThrowIfIdentityUnavailable(source, existingSources, exceptId: null);
        validator.Validate(source, existingSources);
        ValidatePoints(source);
        var now = timeProvider.GetUtcNow();
        var timestamp = Timestamp(now);
        var created = source with
        {
            Revision = 1,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
            Points = StampPoints(source.Points, 1, timestamp, timestamp)
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
        context.PointSourcePoints.AddRange(created.Points.Select(point => new PointSourcePointEntity
        {
            PointId = point.Id,
            SourceId = created.Id
        }));

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraint(exception))
        {
            throw new PointSourceConflictException(
                "A point source with that ID or name already exists. Choose a unique ID and name.",
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

        if (revision != previous.Revision)
        {
            throw new PointSourceConflictException("stale revision");
        }

        var existingSources = await LoadSources(cancellationToken);
        ThrowIfIdentityUnavailable(source, existingSources, id);
        validator.Validate(source, existingSources.Where(existing => existing.Id != id));
        ValidatePoints(source);
        var now = timeProvider.GetUtcNow();
        var timestamp = Timestamp(now);
        var updated = source with
        {
            Revision = previous.Revision + 1,
            CreatedAt = previous.CreatedAt,
            UpdatedAt = timestamp,
            Points = StampUpdatedPoints(
                source.Points,
                previous.Points,
                previous.Revision + 1,
                timestamp)
        };

        if (source.Id != id)
        {
            await Rename(entity, updated, cancellationToken);

            return updated;
        }

        entity.Json = Serialize(updated);
        entity.Key = NormalizeName(updated.Name);
        entity.Updated = now;
        var oldOwnership = await context.PointSourcePoints
            .Where(item => item.SourceId == id)
            .ToListAsync(cancellationToken);
        var updatedPointIds = updated.Points.Select(point => point.Id).ToHashSet(StringComparer.Ordinal);
        var existingPointIds = oldOwnership.Select(item => item.PointId).ToHashSet(StringComparer.Ordinal);
        context.PointSourcePoints.RemoveRange(
            oldOwnership.Where(item => !updatedPointIds.Contains(item.PointId)));
        context.PointSourcePoints.AddRange(updated.Points
            .Where(point => !existingPointIds.Contains(point.Id))
            .Select(point => new PointSourcePointEntity
            {
                PointId = point.Id,
                SourceId = id
            }));

        try
        {
            await SaveWithConcurrencyMapping(entity, cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraint(exception))
        {
            throw new PointSourceConflictException(
                "A point source with that name already exists. Choose a different name.",
                exception);
        }

        return updated;
    }

    private async Task Rename(
        PointSourceEntity previous,
        PointSource updated,
        CancellationToken cancellationToken)
    {
        try
        {
            await context.ExecuteInTransactionAsync(
                async transactionCancellationToken =>
                {
                    // Free the unique name key before inserting the replacement. All steps are
                    // committed together, so callers can never observe both source IDs or neither.
                    previous.Key = $"renaming-{Guid.NewGuid():N}";
                    await SaveWithConcurrencyMapping(previous, transactionCancellationToken);

                    var replacement = new PointSourceEntity
                    {
                        Id = updated.Id,
                        Key = NormalizeName(updated.Name),
                        Json = Serialize(updated),
                        Created = previous.Created,
                        Updated = timeProvider.GetUtcNow()
                    };
                    context.PointSources.Add(replacement);
                    await context.SaveChangesAsync(transactionCancellationToken);

                    await context.PointSourcePoints
                        .Where(item => item.SourceId == previous.Id)
                        .ExecuteUpdateAsync(
                            setters => setters.SetProperty(item => item.SourceId, updated.Id),
                            transactionCancellationToken);

                    context.PointSources.Remove(previous);
                    await SaveWithConcurrencyMapping(replacement, transactionCancellationToken);
                },
                cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraint(exception))
        {
            throw new PointSourceConflictException(
                $"A point source with ID \"{updated.Id}\" already exists. "
                + "Choose a different ID.",
                exception);
        }
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

        var source = Deserialize(entity);

        if (await IsReferencedByFlow(source.Points.Select(point => point.Id), cancellationToken))
        {
            throw new PointSourceConflictException(
                "one or more source points are referenced by a flow");
        }

        context.PointSources.Remove(entity);
        await SaveWithConcurrencyMapping(entity: null, cancellationToken);
    }

    private static void ThrowIfIdentityUnavailable(
        PointSource source,
        IEnumerable<PointSource> existingSources,
        string? exceptId)
    {
        var otherSources = existingSources.Where(existing => existing.Id != exceptId).ToList();
        var idConflict = otherSources.FirstOrDefault(existing => existing.Id == source.Id);

        if (idConflict is not null)
        {
            throw DuplicateId(source.Id, idConflict.Name);
        }

        var nameConflict = otherSources.FirstOrDefault(existing =>
            string.Equals(existing.Name, source.Name, StringComparison.OrdinalIgnoreCase));

        if (nameConflict is not null)
        {
            throw new PointSourceConflictException(
                $"A point source named \"{nameConflict.Name}\" already exists with ID "
                + $"\"{nameConflict.Id}\". Choose a different name.");
        }
    }

    private static PointSourceConflictException DuplicateId(string id, string name) =>
        new($"A point source with ID \"{id}\" already exists with name \"{name}\". "
            + "Choose a different ID.");

    private async Task<PointSourceEntity> FindTracked(
        string id,
        CancellationToken cancellationToken) =>
        await context.PointSources.SingleOrDefaultAsync(
            source => source.Id == id,
            cancellationToken)
        ?? throw new PointSourceNotFoundException(id);

    private async Task<bool> IsReferencedByFlow(
        IEnumerable<string> pointIds,
        CancellationToken cancellationToken)
    {
        var ids = pointIds.ToHashSet(StringComparer.Ordinal);

        if (ids.Count == 0)
        {
            return false;
        }

        foreach (var entity in await context.Flows.AsNoTracking().ToListAsync(cancellationToken))
        {
            using var document = JsonDocument.Parse(entity.Json);

            if (document.RootElement.TryGetProperty("nodes", out var nodes))
            {
                foreach (var node in nodes.EnumerateArray())
                {
                    if (node.TryGetProperty("configuration", out var configuration)
                        && configuration.TryGetProperty("pointId", out var pointId)
                        && pointId.ValueKind == JsonValueKind.String
                        && ids.Contains(pointId.GetString()!))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private async Task<IReadOnlyList<PointSource>> LoadSources(CancellationToken cancellationToken) =>
        [.. (await context.PointSources.AsNoTracking().ToListAsync(cancellationToken)).Select(Deserialize)];

    private void ValidatePoints(PointSource source)
    {
        var validationContext = new PointValidationContext(
            new Dictionary<string, PointSource>(StringComparer.Ordinal)
            {
                [source.Id] = source
            });

        foreach (var point in source.Points)
        {
            pointValidator.Validate(point, validationContext);
        }
    }

    private static IReadOnlyList<AutomationPoint> StampPoints(
        IReadOnlyList<AutomationPoint> points,
        int revision,
        string createdAt,
        string updatedAt) =>
        [.. points.Select(point => point with
        {
            Revision = revision,
            CreatedAt = point.CreatedAt ?? createdAt,
            UpdatedAt = updatedAt
        })];

    private static IReadOnlyList<AutomationPoint> StampUpdatedPoints(
        IReadOnlyList<AutomationPoint> points,
        IReadOnlyList<AutomationPoint> previousPoints,
        int revision,
        string timestamp)
    {
        var previousById = previousPoints.ToDictionary(point => point.Id, StringComparer.Ordinal);

        return [.. points.Select(point => point with
        {
            Revision = revision,
            CreatedAt = previousById.TryGetValue(point.Id, out var previous)
                ? previous.CreatedAt ?? timestamp
                : timestamp,
            UpdatedAt = timestamp
        })];
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