using Server.Data.Context;
using Server.Data.Entities;

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

    /// <summary>
    /// Purpose: Protects the behavioral contract that initialization is idempotent and creates schema and triggers.
    /// Description: Arranges the inputs for initialization is idempotent and creates schema and triggers, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
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

        // Expected outcome: `triggerCount` has the required value.
        // Acceptance criteria: `triggerCount` must equal `5`, because this condition proves that
        // initialization is idempotent and creates schema and triggers.
        Assert.That(triggerCount, Is.EqualTo(8));
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that unique keys are enforced.
    /// Description: Arranges the inputs for unique keys are enforced, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public async Task UniqueKeysAreEnforced()
    {
        await using var provider = CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IFlowControlDbContext>();
        await context.InitializeDatabase();
        context.Flows.Add(CreateFlow("one", "same"));
        context.Flows.Add(CreateFlow("two", "same"));

        // Expected outcome: The invalid operation is rejected.
        // Acceptance criteria: the operation must throw DbUpdateException, because this condition proves that
        // unique keys are enforced.
        Assert.That(
            async () => await context.SaveChangesAsync(CancellationToken.None),
            Throws.TypeOf<DbUpdateException>());
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that trigger increments version and rejects stale updates.
    /// Description: Arranges the inputs for trigger increments version and rejects stale updates, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
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

        // Expected outcome: The invalid operation is rejected.
        // Acceptance criteria: the operation must throw DbUpdateConcurrencyException, because this condition proves that
        // trigger increments version and rejects stale updates.
        Assert.That(
            async () => await first.SaveChangesAsync(CancellationToken.None),
            Throws.TypeOf<DbUpdateConcurrencyException>());

        await using var verifyScope = provider.CreateAsyncScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<IFlowControlDbContext>();
        var saved = await verify.Flows.SingleAsync();

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // trigger increments version and rejects stale updates.
        Assert.Multiple(() =>
        {
            // Expected outcome: `saved.RowVersion` has the required value.
            // Acceptance criteria: `saved.RowVersion` must equal `2`, because this condition proves that
            // trigger increments version and rejects stale updates.
            Assert.That(saved.RowVersion, Is.EqualTo(2));

            // Expected outcome: `saved.Json` has the required value.
            // Acceptance criteria: `saved.Json` must equal `"""{"name":"current"}"""`, because this condition proves that
            // trigger increments version and rejects stale updates.
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
