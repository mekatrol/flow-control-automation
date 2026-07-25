using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Server.Data;
using Server.Data.Context;
using Server.Data.Entities;
using Server.Data.Extensions;

namespace Tests.Unit.Data;

public sealed class DatabaseTests
{
    private string _temporaryDirectory = null!;
    private string _connectionString = null!;

    [SetUp]
    public void SetUp()
    {
        _temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"flow-control-data-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_temporaryDirectory);
        _connectionString = $"Data Source={Path.Combine(_temporaryDirectory, "database.db")};Pooling=False";
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }

    [Test]
    public async Task InitializationIsIdempotentAndCreatesSchemaAndTriggers()
    {
        await using var provider = CreateProvider();
        await using (var scope = provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IFlowControlDbContext>();
            await context.InitializeDatabase();
            await context.InitializeDatabase();
        }

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'trigger' AND name LIKE 'UpdateRowVersion%'";
        var triggerCount = Convert.ToInt32(await command.ExecuteScalarAsync());
        Assert.That(triggerCount, Is.EqualTo(5));
    }

    [Test]
    public async Task UniqueKeysAreEnforced()
    {
        await using var provider = CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IFlowControlDbContext>();
        await context.InitializeDatabase();
        context.Flows.Add(CreateFlow("one", "same"));
        context.Flows.Add(CreateFlow("two", "same"));

        Assert.That(
            async () => await context.SaveChangesAsync(CancellationToken.None),
            Throws.TypeOf<DbUpdateException>());
    }

    [Test]
    public async Task TriggerIncrementsVersionAndRejectsStaleUpdates()
    {
        await using var provider = CreateProvider();
        await using (var setupScope = provider.CreateAsyncScope())
        {
            var setup = setupScope.ServiceProvider.GetRequiredService<IFlowControlDbContext>();
            await setup.InitializeDatabase();
            setup.Flows.Add(CreateFlow("one", "flow"));
            await setup.SaveChangesAsync(CancellationToken.None);
        }

        await using var firstScope = provider.CreateAsyncScope();
        await using var secondScope = provider.CreateAsyncScope();
        var first = firstScope.ServiceProvider.GetRequiredService<IFlowControlDbContext>();
        var second = secondScope.ServiceProvider.GetRequiredService<IFlowControlDbContext>();
        var staleEntity = await first.Flows.SingleAsync();
        var currentEntity = await second.Flows.SingleAsync();
        currentEntity.Json = """{"name":"current"}""";
        await second.SaveChangesAsync(CancellationToken.None);
        staleEntity.Json = """{"name":"stale"}""";

        Assert.That(
            async () => await first.SaveChangesAsync(CancellationToken.None),
            Throws.TypeOf<DbUpdateConcurrencyException>());

        await using var verifyScope = provider.CreateAsyncScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<IFlowControlDbContext>();
        var saved = await verify.Flows.SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That(saved.RowVersion, Is.EqualTo(2));
            Assert.That(saved.Json, Is.EqualTo("""{"name":"current"}"""));
        });
    }

    private ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{DatabaseOptions.SectionName}:{DatabaseOptions.FlowControlConfigurationKey}"] =
                    _connectionString
            })
            .Build();
        services.AddFlowControlData(configuration);
        return services.BuildServiceProvider();
    }

    private static FlowEntity CreateFlow(string id, string key) => new()
    {
        Id = id,
        Key = key,
        Json = "{}",
        Created = DateTimeOffset.UtcNow,
        Updated = DateTimeOffset.UtcNow
    };
}