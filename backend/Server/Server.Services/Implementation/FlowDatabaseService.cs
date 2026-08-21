using Server.Common.Contracts;
using Server.Compiler;
using Server.Compiler.Services;
using Server.Data.Context;
using Server.Data.Entities;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Server.Services.Implementation;

internal sealed class FlowDatabaseService(
    IFlowControlDbContext context,
    IFlowValidator flowValidator,
    TimeProvider timeProvider) : IFlowStore
{
    public async Task<PaginatedResult<Flow>> ListAsync(
        FlowListOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.PageSize is not (10 or 20 or 50))
        {
            throw new FlowValidationException("pageSize must be one of 10, 20, or 50");
        }

        if (options.Page < 1)
        {
            throw new FlowValidationException("page must be at least 1");
        }

        if (options.SortDirection is not ("ascending" or "descending"))
        {
            throw new FlowValidationException(
                "sortDirection must be ascending or descending");
        }

        var statuses = options.Statuses ?? [];
        if (statuses.Any(status => status is not ("draft" or "deployed")))
        {
            throw new FlowValidationException("status must be draft or deployed");
        }

        // SQLite's case folding is ASCII-only. Materializing before matching keeps
        // parity with Go's Unicode-aware strings.ToLower behavior.
        var stored = await context.Flows
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var filter = options.Filter.Trim();
        var matches = stored
            .Select(Deserialize)
            .Where(flow =>
                (filter.Length == 0
                    || flow.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                && (statuses.Count == 0 || statuses.Contains(flow.Status, StringComparer.Ordinal)))
            .OrderBy(
                flow => flow,
                options.SortDirection == "descending"
                    ? DescendingFlowComparer.Instance
                    : AscendingFlowComparer.Instance)
            .ToList();

        var pageCount = Math.Max(1, (matches.Count + options.PageSize - 1) / options.PageSize);
        var page = Math.Clamp(options.Page, 1, pageCount);
        var items = matches
            .Skip((page - 1) * options.PageSize)
            .Take(options.PageSize)
            .ToList();
        return new(items, matches.Count, page, options.PageSize, pageCount);
    }

    public async Task<Flow> GetAsync(string id, CancellationToken cancellationToken)
    {
        var entity = await context.Flows
            .AsNoTracking()
            .SingleOrDefaultAsync(flow => flow.Id == id, cancellationToken);
        return entity is null ? throw new FlowNotFoundException(id) : Deserialize(entity);
    }

    public async Task<Flow> CreateAsync(string name, CancellationToken cancellationToken)
    {
        var trimmedName = name.Trim();
        var baseId = Slug(trimmedName);
        if (baseId.Length == 0)
        {
            baseId = "flow";
        }

        var existingIds = await context.Flows
            .AsNoTracking()
            .Where(flow => flow.Id == baseId || flow.Id.StartsWith(baseId + "-"))
            .Select(flow => flow.Id)
            .ToListAsync(cancellationToken);
        var used = existingIds.ToHashSet(StringComparer.Ordinal);
        var id = baseId;
        for (var suffix = 2; used.Contains(id); suffix++)
        {
            id = $"{baseId}-{suffix}";
        }

        var now = Timestamp();
        var flow = new Flow
        {
            Id = id,
            Name = trimmedName,
            UpdatedAt = now
        };

        flowValidator.Validate(flow);

        context.Flows.Add(new FlowEntity
        {
            Id = id,
            Key = id,
            Json = Serialize(flow),
            Created = timeProvider.GetUtcNow(),
            Updated = timeProvider.GetUtcNow()
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraint(exception))
        {
            // Two creators can select the same readable slug. Surface this as a
            // concurrency conflict rather than silently replacing either flow.
            throw new FlowConcurrencyException(id, exception);
        }

        return flow;
    }

    public async Task<Flow> SaveAsync(
        string id,
        Flow flow,
        CancellationToken cancellationToken)
    {
        var entity = await FindTrackedAsync(id, cancellationToken);
        if (flow.Id != id)
        {
            throw new FlowValidationException("flow id must match the request path");
        }

        var current = Deserialize(entity);
        if (flow.Revision != current.Revision)
        {
            throw new FlowConcurrencyException(id);
        }

        var saved = flow with { UpdatedAt = Timestamp(), Revision = checked(current.Revision + 1) };

        flowValidator.Validate(saved);

        entity.Json = Serialize(saved);
        entity.Updated = timeProvider.GetUtcNow();
        await SaveWithConcurrencyMapping(id, entity, cancellationToken);
        return saved;
    }

    public async Task<Flow> SetDisabledAsync(
        string id,
        bool disabled,
        CancellationToken cancellationToken)
    {
        var entity = await FindTrackedAsync(id, cancellationToken);
        var saved = Deserialize(entity) with
        {
            Disabled = disabled,
            UpdatedAt = Timestamp()
        };
        entity.Json = Serialize(saved);
        entity.Updated = timeProvider.GetUtcNow();
        await SaveWithConcurrencyMapping(id, entity, cancellationToken);
        return saved;
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        var entity = await FindTrackedAsync(id, cancellationToken);
        context.Flows.Remove(entity);
        await SaveWithConcurrencyMapping(id, entity: null, cancellationToken);
    }

    private static Flow Deserialize(FlowEntity entity) =>
        JsonSerializer.Deserialize<Flow>(entity.Json, FlowControlJson.Options)
        ?? throw new InvalidOperationException($"Stored flow {entity.Id} is null.");

    private static string Serialize(Flow flow) =>
        JsonSerializer.Serialize(flow, FlowControlJson.Options);

    private async Task<FlowEntity> FindTrackedAsync(
        string id,
        CancellationToken cancellationToken) =>
        await context.Flows.SingleOrDefaultAsync(flow => flow.Id == id, cancellationToken)
        ?? throw new FlowNotFoundException(id);

    private async Task SaveWithConcurrencyMapping(
        string id,
        FlowEntity? entity,
        CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            if (entity is not null)
            {
                // The SQLite trigger increments RowVersion after EF's update.
                // Reloading synchronizes the tracked token for another operation
                // in the same scope while preserving stale-write detection.
                await context.ReloadAsync(entity, cancellationToken);
            }
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new FlowConcurrencyException(id, exception);
        }
    }

    private string Timestamp() =>
        timeProvider.GetUtcNow().ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK", CultureInfo.InvariantCulture);

    private static string Slug(string name)
    {
        var result = new StringBuilder();
        var dash = false;
        foreach (var rune in name.Trim().ToLowerInvariant().EnumerateRunes())
        {
            if (Rune.IsLetterOrDigit(rune))
            {
                if (dash && result.Length > 0)
                {
                    result.Append('-');
                }

                result.Append(rune);
                dash = false;
            }
            else
            {
                dash = true;
            }
        }

        return result.ToString();
    }

    private static bool IsUniqueConstraint(DbUpdateException exception) =>
        exception.InnerException?.Message.Contains(
            "UNIQUE constraint failed",
            StringComparison.Ordinal) == true;

    private sealed class AscendingFlowComparer : IComparer<Flow>
    {
        public static AscendingFlowComparer Instance { get; } = new();

        public int Compare(Flow? left, Flow? right)
        {
            var comparison = StringComparer.OrdinalIgnoreCase.Compare(left?.Name, right?.Name);
            return comparison != 0
                ? comparison
                : StringComparer.Ordinal.Compare(left?.Id, right?.Id);
        }
    }

    private sealed class DescendingFlowComparer : IComparer<Flow>
    {
        public static DescendingFlowComparer Instance { get; } = new();

        public int Compare(Flow? left, Flow? right) =>
            -AscendingFlowComparer.Instance.Compare(left, right);
    }
}