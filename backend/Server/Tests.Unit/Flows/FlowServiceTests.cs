using Server.Common.Contracts;
using Server.Compiler;
using Server.Compiler.Extensions;
using Server.Data.Context;
using Server.Services;
using Server.Services.Extensions;
using System.Text.Json;

namespace Tests.Unit.Flows;

public sealed class FlowServiceTests
{
    private string _temporaryDirectory = null!;
    private string _connectionString = null!;

    [SetUp]
    public void SetUp()
    {
        _temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"flow-control-flow-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_temporaryDirectory);

        _connectionString = $"Data Source={Path.Combine(_temporaryDirectory, "flows.db")};Pooling=False";
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
    /// Purpose: Protects the behavioral contract that creates readable unique slugs and persists across provider restart.
    /// Description: Arranges the inputs for creates readable unique slugs and persists across provider restart, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public async Task CreatesReadableUniqueSlugsAndPersistsAcrossProviderRestart()
    {
        await using (var provider = await CreateInitializedProvider())
        {
            await using var scope = provider.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IFlowService>();
            var first = await service.CreateAsync(" Heating & Cooling ", CancellationToken.None);
            var second = await service.CreateAsync("Heating & Cooling", CancellationToken.None);

            // Expected outcome: All related outcomes satisfy their contracts.
            // Acceptance criteria: every assertion in the group must pass, because this condition proves that
            // creates readable unique slugs and persists across provider restart.
            Assert.Multiple(() =>
            {
                // Expected outcome: `first.Id` has the required value.
                // Acceptance criteria: `first.Id` must equal `"heating-cooling"`, because this condition proves that
                // creates readable unique slugs and persists across provider restart.
                Assert.That(first.Id, Is.EqualTo("heating-cooling"));

                // Expected outcome: `first.Name` has the required value.
                // Acceptance criteria: `first.Name` must equal `"Heating & Cooling"`, because this condition proves that
                // creates readable unique slugs and persists across provider restart.
                Assert.That(first.Name, Is.EqualTo("Heating & Cooling"));

                // Expected outcome: `second.Id` has the required value.
                // Acceptance criteria: `second.Id` must equal `"heating-cooling-2"`, because this condition proves that
                // creates readable unique slugs and persists across provider restart.
                Assert.That(second.Id, Is.EqualTo("heating-cooling-2"));

                // Expected outcome: `first.Status` has the required value.
                // Acceptance criteria: `first.Status` must equal `"draft"`, because this condition proves that
                // creates readable unique slugs and persists across provider restart.
                Assert.That(first.Status, Is.EqualTo("draft"));

                // Expected outcome: `first.Nodes` contains no entries.
                // Acceptance criteria: `first.Nodes` must be empty, because this condition proves that
                // creates readable unique slugs and persists across provider restart.
                Assert.That(first.Nodes, Is.Empty);
            });
        }

        await using var restarted = await CreateInitializedProvider();
        await using var restartedScope = restarted.CreateAsyncScope();
        var restartedService =
            restartedScope.ServiceProvider.GetRequiredService<IFlowService>();
        var saved = await restartedService.GetAsync(
            "heating-cooling",
            CancellationToken.None);

        // Expected outcome: `saved.Name` has the required value.
        // Acceptance criteria: `saved.Name` must equal `"Heating & Cooling"`, because this condition proves that
        // creates readable unique slugs and persists across provider restart.
        Assert.That(saved.Name, Is.EqualTo("Heating & Cooling"));
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that lists with case insensitive filtering sorting and page clamping.
    /// Description: Arranges the inputs for lists with case insensitive filtering sorting and page clamping, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public async Task ListsWithCaseInsensitiveFilteringSortingAndPageClamping()
    {
        await using var provider = await CreateInitializedProvider();
        foreach (var name in new[] { "zeta", "Alpha", "alpha", "Beta" })
        {
            await using var createScope = provider.CreateAsyncScope();
            await createScope.ServiceProvider
                .GetRequiredService<IFlowService>()
                .CreateAsync(name, CancellationToken.None);
        }

        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IFlowService>();
        var page = await service.ListAsync(
            new("a", Page: 99, PageSize: 10, SortDirection: "ascending"),
            CancellationToken.None);

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // lists with case insensitive filtering sorting and page clamping.
        Assert.Multiple(() =>
        {
            // Expected outcome: `page.Items.Select(flow => flow.Id` has the required value.
            // Acceptance criteria: `page.Items.Select(flow => flow.Id` must equal `new[] { "alpha", "alpha-2", "beta", "zeta" }`, because this condition proves that
            // lists with case insensitive filtering sorting and page clamping.
            Assert.That(page.Items.Select(flow => flow.Id), Is.EqualTo(
                new[] { "alpha", "alpha-2", "beta", "zeta" }));

            // Expected outcome: `page.TotalItems` has the required value.
            // Acceptance criteria: `page.TotalItems` must equal `4`, because this condition proves that
            // lists with case insensitive filtering sorting and page clamping.
            Assert.That(page.TotalItems, Is.EqualTo(4));

            // Expected outcome: `page.Page` has the required value.
            // Acceptance criteria: `page.Page` must equal `1`, because this condition proves that
            // lists with case insensitive filtering sorting and page clamping.
            Assert.That(page.Page, Is.EqualTo(1));

            // Expected outcome: `page.PageCount` has the required value.
            // Acceptance criteria: `page.PageCount` must equal `1`, because this condition proves that
            // lists with case insensitive filtering sorting and page clamping.
            Assert.That(page.PageCount, Is.EqualTo(1));
        });
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that rejects invalid replacement without changing stored flow.
    /// Description: Arranges the inputs for rejects invalid replacement without changing stored flow, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public async Task RejectsInvalidReplacementWithoutChangingStoredFlow()
    {
        await using var provider = await CreateInitializedProvider();
        await using (var createScope = provider.CreateAsyncScope())
        {
            await createScope.ServiceProvider
                .GetRequiredService<IFlowService>()
                .CreateAsync("Safe flow", CancellationToken.None);
        }

        await using (var saveScope = provider.CreateAsyncScope())
        {
            var service = saveScope.ServiceProvider.GetRequiredService<IFlowService>();
            var original = await service.GetAsync("safe-flow", CancellationToken.None);
            var invalid = original with
            {
                Nodes =
                [
                    new FlowNode
                    {
                        Id = "node",
                        Kind = FlowNodeKind.Unknown,
                        Label = "Node"
                    },
                ],
            };

            // Expected outcome: The invalid operation is rejected.
            // Acceptance criteria: the operation must throw FlowValidationException, because this condition proves that
            // rejects invalid replacement without changing stored flow.
            Assert.That(
                async () => await service.SaveAsync(
                    "safe-flow",
                    invalid,
                    CancellationToken.None),
                Throws.TypeOf<FlowValidationException>());
        }

        await using var verifyScope = provider.CreateAsyncScope();
        var saved = await verifyScope.ServiceProvider
            .GetRequiredService<IFlowService>()
            .GetAsync("safe-flow", CancellationToken.None);

        // Expected outcome: `saved.Nodes` contains no entries.
        // Acceptance criteria: `saved.Nodes` must be empty, because this condition proves that
        // rejects invalid replacement without changing stored flow.
        Assert.That(saved.Nodes, Is.Empty);
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that validates connections and scalar configuration.
    /// Description: Arranges the inputs for validates connections and scalar configuration, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public async Task ValidatesConnectionsAndScalarConfiguration()
    {
        await using var provider = await CreateInitializedProvider();
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IFlowService>();
        var created = await service.CreateAsync("Graph", CancellationToken.None);
        var graph = created with
        {
            Nodes =
            [
                Node("source", DataDirection.Output, DataType.Number),
                Node("target", DataDirection.Input, DataType.String),
            ],
            Connections =
            [
                new(
                    "connection",
                    new("source", "connector"),
                    new("target", "connector")),
            ],
        };

        // Expected outcome: The invalid operation is rejected.
        // Acceptance criteria: the operation must throw FlowValidationException, because this condition proves that
        // validates connections and scalar configuration.
        Assert.That(
            async () => await service.SaveAsync(
                graph.Id,
                graph,
                CancellationToken.None),
            Throws.TypeOf<FlowValidationException>()
                .With.Message.EqualTo(
                    "connections[0]: connector data types are incompatible"));

        var nestedConfiguration = graph with
        {
            Connections = [],
            Nodes =
            [
                Node("source", DataDirection.Output, DataType.Number) with
                {
                    Configuration = new Dictionary<string, JsonElement>
                    {
                        ["nested"] = JsonSerializer.Deserialize<JsonElement>("{}")
                    },
                },
            ],
        };

        // Expected outcome: The invalid operation is rejected.
        // Acceptance criteria: the operation must throw FlowValidationException, because this condition proves that
        // validates connections and scalar configuration.
        Assert.That(
            async () => await service.SaveAsync(
                graph.Id,
                nestedConfiguration,
                CancellationToken.None),
            Throws.TypeOf<FlowValidationException>()
                .With.Message.EqualTo(
                    "nodes[0].configuration.nested: value must be a JSON scalar"));
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that requires matching path and supports disable and delete.
    /// Description: Arranges the inputs for requires matching path and supports disable and delete, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public async Task RequiresMatchingPathAndSupportsDisableAndDelete()
    {
        await using var provider = await CreateInitializedProvider();
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IFlowService>();
        var created = await service.CreateAsync("Lifecycle", CancellationToken.None);

        // Expected outcome: The invalid operation is rejected.
        // Acceptance criteria: the operation must throw FlowValidationException, because this condition proves that
        // requires matching path and supports disable and delete.
        Assert.That(
            async () => await service.SaveAsync(
                created.Id,
                created with { Id = "other" },
                CancellationToken.None),
            Throws.TypeOf<FlowValidationException>());

        var disabled = await service.SetDisabledAsync(
            created.Id,
            true,
            CancellationToken.None);

        // Expected outcome: `disabled.Disabled` confirms the required condition.
        // Acceptance criteria: `disabled.Disabled` must be true, because this condition proves that
        // requires matching path and supports disable and delete.
        Assert.That(disabled.Disabled, Is.True);

        await service.DeleteAsync(created.Id, CancellationToken.None);

        // Expected outcome: The invalid operation is rejected.
        // Acceptance criteria: the operation must throw FlowNotFoundException, because this condition proves that
        // requires matching path and supports disable and delete.
        Assert.That(
            async () => await service.GetAsync(created.Id, CancellationToken.None),
            Throws.TypeOf<FlowNotFoundException>());
    }

    private async Task<ServiceProvider> CreateInitializedProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [ServerOptions.AddressConfigurationKey] = "http://127.0.0.1:0",
                [$"{DatabaseOptions.SectionName}:{DatabaseOptions.FlowControlConfigurationKey}"] =
                    _connectionString
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFlowCompilerServices();
        services.AddFlowControlServer(configuration);

        var provider = services.BuildServiceProvider();

        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider
            .GetRequiredService<IFlowControlDbContext>()
            .InitializeDatabase();

        return provider;
    }

    private static FlowNode Node(string id, DataDirection direction, DataType dataType) => new()
    {
        Id = id,
        Kind = FlowNodeKind.And,
        Label = id,
        Connectors =
        [
            new("connector", "Connector", direction, dataType, "right")
        ]
    };
}