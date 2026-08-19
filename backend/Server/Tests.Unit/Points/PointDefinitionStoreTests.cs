using Server.Common.Contracts;
using Server.Common.Services;
using Server.Data.Context;
using Server.Data.Entities;
using Server.Services;
using Server.Services.Contracts;
using System.Text.Json;
using Tests.Unit.Api;

namespace Tests.Unit.Points;

[TestFixture]
internal sealed class PointDefinitionStoreTests
{
    /// <summary>
    /// Purpose: Protects the behavioral contract that empty database supports crud and deterministic listing.
    /// Description: Arranges the inputs for empty database supports crud and deterministic listing, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public async Task EmptyDatabaseSupportsCrudAndDeterministicListing()
    {
        await using var factory = new FlowControlApplicationFactory();
        _ = factory.CreateClient();

        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IPointDefinitionStore>();

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // empty database supports crud and deterministic listing.
        Assert.Multiple(async () =>
        {
            // Expected outcome: `await store.ListPointsAsync(default` contains no entries.
            // Acceptance criteria: `await store.ListPointsAsync(default` must be empty, because this condition proves that
            // empty database supports crud and deterministic listing.
            Assert.That(await store.ListPointsAsync(default), Is.Empty);

            // Expected outcome: `await store.ListGroupsAsync(default` contains no entries.
            // Acceptance criteria: `await store.ListGroupsAsync(default` must be empty, because this condition proves that
            // empty database supports crud and deterministic listing.
            Assert.That(await store.ListGroupsAsync(default), Is.Empty);
        });

        var second = await store.CreateGroupAsync(
            Group("second", "Zulu"),
            default);
        var first = await store.CreateGroupAsync(
            Group("first", "alpha"),
            default);
        var created = await store.CreatePointAsync(
            VirtualPoint("point", "Point", first.Id),
            default);
        var updated = await store.UpdatePointAsync(
            created.Id,
            created with { Description = "Updated" },
            created.Revision,
            default);

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // empty database supports crud and deterministic listing.
        Assert.Multiple(() =>
        {
            // Expected outcome: `created.Revision` has the required value.
            // Acceptance criteria: `created.Revision` must equal `1`, because this condition proves that
            // empty database supports crud and deterministic listing.
            Assert.That(created.Revision, Is.EqualTo(1));

            // Expected outcome: `created.CreatedAt` is available.
            // Acceptance criteria: `created.CreatedAt` must not be null, because this condition proves that
            // empty database supports crud and deterministic listing.
            Assert.That(created.CreatedAt, Is.Not.Null);

            // Expected outcome: `updated.Revision` has the required value.
            // Acceptance criteria: `updated.Revision` must equal `2`, because this condition proves that
            // empty database supports crud and deterministic listing.
            Assert.That(updated.Revision, Is.EqualTo(2));

            // Expected outcome: `updated.CreatedAt` has the required value.
            // Acceptance criteria: `updated.CreatedAt` must equal `created.CreatedAt`, because this condition proves that
            // empty database supports crud and deterministic listing.
            Assert.That(updated.CreatedAt, Is.EqualTo(created.CreatedAt));

            // Expected outcome: The observed result satisfies the required contract.
            // Acceptance criteria: the asserted condition must hold, because this condition proves that
            // empty database supports crud and deterministic listing.
            Assert.That(updated.UpdatedAt, Is.Not.Null);

            // Expected outcome: `second.Revision` has the required value.
            // Acceptance criteria: `second.Revision` must equal `1`, because this condition proves that
            // empty database supports crud and deterministic listing.
            Assert.That(second.Revision, Is.EqualTo(1));
        });

        // Expected outcome: `(await store.ListGroupsAsync(default` has the required value.
        // Acceptance criteria: `(await store.ListGroupsAsync(default` must equal `new[] { "first", "second" }`, because this condition proves that
        // empty database supports crud and deterministic listing.
        Assert.That(
            (await store.ListGroupsAsync(default)).Select(group => group.Id),
            Is.EqualTo(new[] { "first", "second" }));

        await store.DeletePointAsync(updated.Id, updated.Revision, default);
        await store.DeleteGroupAsync(first.Id, first.Revision, default);
        await store.DeleteGroupAsync(second.Id, second.Revision, default);

        // Expected outcome: `await store.ListGroupsAsync(default` contains no entries.
        // Acceptance criteria: `await store.ListGroupsAsync(default` must be empty, because this condition proves that
        // empty database supports crud and deterministic listing.
        Assert.That(await store.ListGroupsAsync(default), Is.Empty);
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that definitions survive aservice scope and application restart.
    /// Description: Arranges the inputs for definitions survive aservice scope and application restart, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public async Task DefinitionsSurviveAServiceScopeAndApplicationRestart()
    {
        await using var factory = new FlowControlApplicationFactory();
        _ = factory.CreateClient();
        await using (var createScope = factory.Services.CreateAsyncScope())
        {
            var store = createScope.ServiceProvider
                .GetRequiredService<IPointDefinitionStore>();
            await store.CreatePointAsync(VirtualPoint("persisted", "Persisted"), default);
        }

        await using var reopenScope = factory.Services.CreateAsyncScope();
        var reopened = reopenScope.ServiceProvider.GetRequiredService<IPointDefinitionStore>();
        var point = await reopened.GetPointAsync("persisted", default);

        // Expected outcome: `point.Name` has the required value.
        // Acceptance criteria: `point.Name` must equal `"Persisted"`, because this condition proves that
        // definitions survive aservice scope and application restart.
        Assert.That(point.Name, Is.EqualTo("Persisted"));
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that duplicate names and stale revisions are rejected.
    /// Description: Arranges the inputs for duplicate names and stale revisions are rejected, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public async Task DuplicateNamesAndStaleRevisionsAreRejected()
    {
        await using var factory = new FlowControlApplicationFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IPointDefinitionStore>();
        var created = await store.CreatePointAsync(VirtualPoint("one", "Same"), default);

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // duplicate names and stale revisions are rejected.
        Assert.Multiple(async () =>
        {
            // Expected outcome: The invalid operation is rejected.
            // Acceptance criteria: the operation must throw PointDefinitionConflictException, because this condition proves that
            // duplicate names and stale revisions are rejected.
            Assert.That(
                async () => await store.CreatePointAsync(
                    VirtualPoint("two", "same"),
                    default),
                Throws.TypeOf<PointDefinitionConflictException>());

            // Expected outcome: The invalid operation is rejected.
            // Acceptance criteria: the operation must throw PointDefinitionConflictException, because this condition proves that
            // duplicate names and stale revisions are rejected.
            Assert.That(
                async () => await store.UpdatePointAsync(
                    created.Id,
                    created with { Name = "Changed" },
                    revision: 0,
                    default),
                Throws.TypeOf<PointDefinitionConflictException>());

            // Expected outcome: The invalid operation is rejected.
            // Acceptance criteria: the operation must throw PointDefinitionConflictException, because this condition proves that
            // duplicate names and stale revisions are rejected.
            Assert.That(
                async () => await store.DeletePointAsync(created.Id, 0, default),
                Throws.TypeOf<PointDefinitionConflictException>());
        });
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that group deletion is blocked until members are made standalone atomically.
    /// Description: Arranges the inputs for group deletion is blocked until members are made standalone atomically, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public async Task GroupDeletionIsBlockedUntilMembersAreMadeStandaloneAtomically()
    {
        await using var factory = new FlowControlApplicationFactory();
        _ = factory.CreateClient();
        await InsertSource(factory, Source("http", "httpJson"));
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IPointDefinitionStore>();
        var group = await store.CreateGroupAsync(
            Group("plant", "Plant") with { SourceId = "http" },
            default);
        var point = await store.CreatePointAsync(BoundPoint("sensor", group.Id), default);

        // Expected outcome: The invalid operation is rejected.
        // Acceptance criteria: the operation must throw PointDefinitionConflictException, because this condition proves that
        // group deletion is blocked until members are made standalone atomically.
        Assert.That(
            async () => await store.DeleteGroupAsync(group.Id, group.Revision, default),
            Throws.TypeOf<PointDefinitionConflictException>());

        var standalone = await store.MakePointsStandaloneAsync(
            group.Id,
            group.Revision,
            default);

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // group deletion is blocked until members are made standalone atomically.
        Assert.Multiple(() =>
        {
            // Expected outcome: `standalone` contains the required number of entries.
            // Acceptance criteria: `standalone` must contain exactly 1 entries, because this condition proves that
            // group deletion is blocked until members are made standalone atomically.
            Assert.That(standalone, Has.Count.EqualTo(1));

            // Expected outcome: `standalone[0].GroupId` is absent.
            // Acceptance criteria: `standalone[0].GroupId` must be null, because this condition proves that
            // group deletion is blocked until members are made standalone atomically.
            Assert.That(standalone[0].GroupId, Is.Null);

            // Expected outcome: `standalone[0].SourceId` has the required value.
            // Acceptance criteria: `standalone[0].SourceId` must equal `"http"`, because this condition proves that
            // group deletion is blocked until members are made standalone atomically.
            Assert.That(standalone[0].SourceId, Is.EqualTo("http"));

            // Expected outcome: `standalone[0].Revision` has the required value.
            // Acceptance criteria: `standalone[0].Revision` must equal `point.Revision + 1`, because this condition proves that
            // group deletion is blocked until members are made standalone atomically.
            Assert.That(standalone[0].Revision, Is.EqualTo(point.Revision + 1));
        });
        await store.DeleteGroupAsync(group.Id, group.Revision, default);
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that group source change rolls back when it would invalidate members.
    /// Description: Arranges the inputs for group source change rolls back when it would invalidate members, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public async Task GroupSourceChangeRollsBackWhenItWouldInvalidateMembers()
    {
        await using var factory = new FlowControlApplicationFactory();
        _ = factory.CreateClient();
        await InsertSource(factory, Source("http", "httpJson"));
        await InsertSource(factory, Source("mqtt", "mqtt"));
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IPointDefinitionStore>();
        var group = await store.CreateGroupAsync(
            Group("plant", "Plant") with { SourceId = "http" },
            default);
        _ = await store.CreatePointAsync(BoundPoint("sensor", group.Id), default);

        // Expected outcome: The invalid operation is rejected.
        // Acceptance criteria: the operation must throw PointDefinitionValidationException, because this condition proves that
        // group source change rolls back when it would invalidate members.
        Assert.That(
            async () => await store.UpdateGroupAsync(
                group.Id,
                group with { SourceId = "mqtt" },
                group.Revision,
                default),
            Throws.TypeOf<PointDefinitionValidationException>());

        // Expected outcome: `(await store.GetGroupAsync(group.Id` has the required value.
        // Acceptance criteria: `(await store.GetGroupAsync(group.Id` must equal `"http"`, because this condition proves that
        // group source change rolls back when it would invalidate members.
        Assert.That(
            (await store.GetGroupAsync(group.Id, default)).SourceId,
            Is.EqualTo("http"));
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that referenced sources cannot change kind or be deleted.
    /// Description: Arranges the inputs for referenced sources cannot change kind or be deleted, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public async Task ReferencedSourcesCannotChangeKindOrBeDeleted()
    {
        await using var factory = new FlowControlApplicationFactory();
        _ = factory.CreateClient();
        var source = Source("http", "httpJson");
        await InsertSource(factory, source);
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IPointDefinitionStore>();
        _ = await store.CreatePointAsync(
            BoundPoint("sensor", groupId: null) with { SourceId = source.Id },
            default);
        var sources = scope.ServiceProvider.GetRequiredService<IPointSourceService>();

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // referenced sources cannot change kind or be deleted.
        Assert.Multiple(async () =>
        {
            // Expected outcome: The invalid operation is rejected.
            // Acceptance criteria: the operation must throw PointSourceConflictException, because this condition proves that
            // referenced sources cannot change kind or be deleted.
            Assert.That(
                async () => await sources.UpdateAsync(
                    source.Id,
                    source with { Kind = "mqtt" },
                    revision: 0,
                    default),
                Throws.TypeOf<PointSourceConflictException>());

            // Expected outcome: The invalid operation is rejected.
            // Acceptance criteria: the operation must throw PointSourceConflictException, because this condition proves that
            // referenced sources cannot change kind or be deleted.
            Assert.That(
                async () => await sources.DeleteAsync(source.Id, revision: 0, default),
                Throws.TypeOf<PointSourceConflictException>());
        });
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that concurrent updates reject the stale writer.
    /// Description: Arranges the inputs for concurrent updates reject the stale writer, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public async Task ConcurrentUpdatesRejectTheStaleWriter()
    {
        await using var factory = new FlowControlApplicationFactory();
        _ = factory.CreateClient();
        FlowPoint created;
        await using (var setupScope = factory.Services.CreateAsyncScope())
        {
            created = await setupScope.ServiceProvider
                .GetRequiredService<IPointDefinitionStore>()
                .CreatePointAsync(VirtualPoint("point", "Point"), default);
        }

        await using var firstScope = factory.Services.CreateAsyncScope();
        await using var secondScope = factory.Services.CreateAsyncScope();
        var first = firstScope.ServiceProvider.GetRequiredService<IPointDefinitionStore>();
        var second = secondScope.ServiceProvider.GetRequiredService<IPointDefinitionStore>();
        _ = await first.GetPointAsync(created.Id, default);
        _ = await second.GetPointAsync(created.Id, default);
        _ = await first.UpdatePointAsync(
            created.Id,
            created with { Description = "First" },
            created.Revision,
            default);

        // Expected outcome: The invalid operation is rejected.
        // Acceptance criteria: the operation must throw PointDefinitionConflictException, because this condition proves that
        // concurrent updates reject the stale writer.
        Assert.That(
            async () => await second.UpdatePointAsync(
                created.Id,
                created with { Description = "Second" },
                created.Revision,
                default),
            Throws.TypeOf<PointDefinitionConflictException>());
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that startup rejects corrupt stored json.
    /// Description: Arranges the inputs for startup rejects corrupt stored json, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public async Task StartupRejectsCorruptStoredJson()
    {
        await using var factory = new FlowControlApplicationFactory();
        _ = factory.CreateClient();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IFlowControlDbContext>();
            context.Points.Add(new PointEntity
            {
                Id = "corrupt",
                Key = "CORRUPT",
                Json = "{",
                Created = DateTimeOffset.UtcNow,
                Updated = DateTimeOffset.UtcNow
            });
            await context.SaveChangesAsync(default);
        }

        await using var validationScope = factory.Services.CreateAsyncScope();
        var startup = validationScope.ServiceProvider
            .GetRequiredService<IStartupDataValidator>();

        // Expected outcome: The invalid operation is rejected.
        // Acceptance criteria: the operation must throw JsonException, because this condition proves that
        // startup rejects corrupt stored json.
        Assert.That(
            async () => await startup.ValidateAsync(default),
            Throws.TypeOf<JsonException>());
    }

    private static async Task InsertSource(
        FlowControlApplicationFactory factory,
        PointSource source)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IFlowControlDbContext>();
        var now = DateTimeOffset.UtcNow;
        context.PointSources.Add(new PointSourceEntity
        {
            Id = source.Id,
            Key = source.Name.ToUpperInvariant(),
            Json = JsonSerializer.Serialize(source, FlowControlJson.Options),
            Created = now,
            Updated = now
        });
        await context.SaveChangesAsync(default);
    }

    private static PointGroup Group(string id, string name) => new()
    {
        Id = id,
        Name = name
    };

    private static FlowPoint VirtualPoint(
        string id,
        string name,
        string? groupId = null) => new()
        {
            Id = id,
            Name = name,
            Enabled = true,
            GroupId = groupId,
            Implementation = "virtual",
            Direction = DataDirection.Value,
            ValueType = FlowPointValueType.Analog,
            Readable = true,
            Persistence = "volatile"
        };

    private static FlowPoint BoundPoint(string id, string? groupId) => new()
    {
        Id = id,
        Name = id,
        Enabled = true,
        GroupId = groupId,
        Implementation = "bound",
        Direction = DataDirection.Input,
        ValueType = FlowPointValueType.Analog,
        Readable = true,
        Persistence = "volatile",
        Mapping = new System.Text.Json.Nodes.JsonObject
        {
            ["path"] = "/value",
            ["method"] = "GET"
        }
    };

    private static PointSource Source(string id, string kind) => new()
    {
        Id = id,
        Name = id,
        Enabled = true,
        Kind = kind,
        Connection = new PointSourceConnection()
    };
}