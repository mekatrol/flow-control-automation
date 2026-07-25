using Microsoft.Extensions.DependencyInjection;
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
    [Test]
    public async Task EmptyDatabaseSupportsCrudAndDeterministicListing()
    {
        using var factory = new FlowControlApplicationFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IPointDefinitionStore>();

        Assert.Multiple(async () =>
        {
            Assert.That(await store.ListPointsAsync(default), Is.Empty);
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

        Assert.Multiple(() =>
        {
            Assert.That(created.Revision, Is.EqualTo(1));
            Assert.That(created.CreatedAt, Is.Not.Null);
            Assert.That(updated.Revision, Is.EqualTo(2));
            Assert.That(updated.CreatedAt, Is.EqualTo(created.CreatedAt));
            Assert.That(updated.UpdatedAt, Is.Not.EqualTo(null));
            Assert.That(second.Revision, Is.EqualTo(1));
        });

        Assert.That(
            (await store.ListGroupsAsync(default)).Select(group => group.Id),
            Is.EqualTo(new[] { "first", "second" }));

        await store.DeletePointAsync(updated.Id, updated.Revision, default);
        await store.DeleteGroupAsync(first.Id, first.Revision, default);
        await store.DeleteGroupAsync(second.Id, second.Revision, default);
        Assert.That(await store.ListGroupsAsync(default), Is.Empty);
    }

    [Test]
    public async Task DefinitionsSurviveAServiceScopeAndApplicationRestart()
    {
        using var factory = new FlowControlApplicationFactory();
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

        Assert.That(point.Name, Is.EqualTo("Persisted"));
    }

    [Test]
    public async Task DuplicateNamesAndStaleRevisionsAreRejected()
    {
        using var factory = new FlowControlApplicationFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IPointDefinitionStore>();
        var created = await store.CreatePointAsync(VirtualPoint("one", "Same"), default);

        Assert.Multiple(async () =>
        {
            Assert.That(
                async () => await store.CreatePointAsync(
                    VirtualPoint("two", "same"),
                    default),
                Throws.TypeOf<PointDefinitionConflictException>());
            Assert.That(
                async () => await store.UpdatePointAsync(
                    created.Id,
                    created with { Name = "Changed" },
                    revision: 0,
                    default),
                Throws.TypeOf<PointDefinitionConflictException>());
            Assert.That(
                async () => await store.DeletePointAsync(created.Id, 0, default),
                Throws.TypeOf<PointDefinitionConflictException>());
        });
    }

    [Test]
    public async Task GroupDeletionIsBlockedUntilMembersAreMadeStandaloneAtomically()
    {
        using var factory = new FlowControlApplicationFactory();
        _ = factory.CreateClient();
        await InsertSource(factory, Source("http", "http_json"));
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IPointDefinitionStore>();
        var group = await store.CreateGroupAsync(
            Group("plant", "Plant") with { SourceId = "http" },
            default);
        var point = await store.CreatePointAsync(BoundPoint("sensor", group.Id), default);

        Assert.That(
            async () => await store.DeleteGroupAsync(group.Id, group.Revision, default),
            Throws.TypeOf<PointDefinitionConflictException>());

        var standalone = await store.MakePointsStandaloneAsync(
            group.Id,
            group.Revision,
            default);

        Assert.Multiple(() =>
        {
            Assert.That(standalone, Has.Count.EqualTo(1));
            Assert.That(standalone[0].GroupId, Is.Null);
            Assert.That(standalone[0].SourceId, Is.EqualTo("http"));
            Assert.That(standalone[0].Revision, Is.EqualTo(point.Revision + 1));
        });
        await store.DeleteGroupAsync(group.Id, group.Revision, default);
    }

    [Test]
    public async Task GroupSourceChangeRollsBackWhenItWouldInvalidateMembers()
    {
        using var factory = new FlowControlApplicationFactory();
        _ = factory.CreateClient();
        await InsertSource(factory, Source("http", "http_json"));
        await InsertSource(factory, Source("mqtt", "mqtt"));
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IPointDefinitionStore>();
        var group = await store.CreateGroupAsync(
            Group("plant", "Plant") with { SourceId = "http" },
            default);
        _ = await store.CreatePointAsync(BoundPoint("sensor", group.Id), default);

        Assert.That(
            async () => await store.UpdateGroupAsync(
                group.Id,
                group with { SourceId = "mqtt" },
                group.Revision,
                default),
            Throws.TypeOf<PointDefinitionValidationException>());
        Assert.That(
            (await store.GetGroupAsync(group.Id, default)).SourceId,
            Is.EqualTo("http"));
    }

    [Test]
    public async Task ReferencedSourcesCannotChangeKindOrBeDeleted()
    {
        using var factory = new FlowControlApplicationFactory();
        _ = factory.CreateClient();
        var source = Source("http", "http_json");
        await InsertSource(factory, source);
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IPointDefinitionStore>();
        _ = await store.CreatePointAsync(
            BoundPoint("sensor", groupId: null) with { SourceId = source.Id },
            default);
        var sources = scope.ServiceProvider.GetRequiredService<IPointSourceService>();

        Assert.Multiple(async () =>
        {
            Assert.That(
                async () => await sources.UpdateAsync(
                    source.Id,
                    source with { Kind = "mqtt" },
                    revision: 0,
                    default),
                Throws.TypeOf<PointSourceConflictException>());
            Assert.That(
                async () => await sources.DeleteAsync(source.Id, revision: 0, default),
                Throws.TypeOf<PointSourceConflictException>());
        });
    }

    [Test]
    public async Task ConcurrentUpdatesRejectTheStaleWriter()
    {
        using var factory = new FlowControlApplicationFactory();
        _ = factory.CreateClient();
        Point created;
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

        Assert.That(
            async () => await second.UpdatePointAsync(
                created.Id,
                created with { Description = "Second" },
                created.Revision,
                default),
            Throws.TypeOf<PointDefinitionConflictException>());
    }

    [Test]
    public async Task StartupRejectsCorruptStoredJson()
    {
        using var factory = new FlowControlApplicationFactory();
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
                Updated = DateTimeOffset.UtcNow,
            });
            await context.SaveChangesAsync(default);
        }

        await using var validationScope = factory.Services.CreateAsyncScope();
        var startup = validationScope.ServiceProvider
            .GetRequiredService<IStartupDataValidator>();
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
            Updated = now,
        });
        await context.SaveChangesAsync(default);
    }

    private static PointGroup Group(string id, string name) => new()
    {
        Id = id,
        Name = name,
    };

    private static Point VirtualPoint(
        string id,
        string name,
        string? groupId = null) => new()
        {
            Id = id,
            Name = name,
            Enabled = true,
            GroupId = groupId,
            Implementation = "virtual",
            Direction = "value",
            ValueType = "analog",
            Readable = true,
            Persistence = "volatile",
        };

    private static Point BoundPoint(string id, string? groupId) => new()
    {
        Id = id,
        Name = id,
        Enabled = true,
        GroupId = groupId,
        Implementation = "bound",
        Direction = "input",
        ValueType = "analog",
        Readable = true,
        Persistence = "volatile",
        Mapping = new System.Text.Json.Nodes.JsonObject
        {
            ["path"] = "/value",
            ["method"] = "GET",
        },
    };

    private static PointSource Source(string id, string kind) => new()
    {
        Id = id,
        Name = id,
        Enabled = true,
        Kind = kind,
        Connection = new PointSourceConnection(),
    };
}