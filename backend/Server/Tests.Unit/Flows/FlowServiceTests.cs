using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Server.Data;
using Server.Data.Context;
using Server.Services;
using Server.Services.Contracts;
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

    [Test]
    public async Task CreatesReadableUniqueSlugsAndPersistsAcrossProviderRestart()
    {
        await using (var provider = await CreateInitializedProvider())
        {
            await using var scope = provider.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IFlowService>();
            var first = await service.CreateAsync(" Heating & Cooling ", CancellationToken.None);
            var second = await service.CreateAsync("Heating & Cooling", CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(first.Id, Is.EqualTo("heating-cooling"));
                Assert.That(first.Name, Is.EqualTo("Heating & Cooling"));
                Assert.That(second.Id, Is.EqualTo("heating-cooling-2"));
                Assert.That(first.Status, Is.EqualTo("draft"));
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
        Assert.That(saved.Name, Is.EqualTo("Heating & Cooling"));
    }

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

        Assert.Multiple(() =>
        {
            Assert.That(page.Items.Select(flow => flow.Id), Is.EqualTo(
                new[] { "alpha", "alpha-2", "beta", "zeta" }));
            Assert.That(page.TotalItems, Is.EqualTo(4));
            Assert.That(page.Page, Is.EqualTo(1));
            Assert.That(page.PageCount, Is.EqualTo(1));
        });
    }

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
                        Kind = "unknown",
                        Label = "Node"
                    },
                ],
            };

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
        Assert.That(saved.Nodes, Is.Empty);
    }

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
                Node("source", "output", "number"),
                Node("target", "input", "string"),
            ],
            Connections =
            [
                new(
                    "connection",
                    new("source", "connector"),
                    new("target", "connector")),
            ],
        };

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
                Node("source", "output", "number") with
                {
                    Configuration = new Dictionary<string, JsonElement>
                    {
                        ["nested"] = JsonSerializer.Deserialize<JsonElement>("{}")
                    },
                },
            ],
        };
        Assert.That(
            async () => await service.SaveAsync(
                graph.Id,
                nestedConfiguration,
                CancellationToken.None),
            Throws.TypeOf<FlowValidationException>()
                .With.Message.EqualTo(
                    "nodes[0].configuration.nested: value must be a JSON scalar"));
    }

    [Test]
    public async Task RequiresMatchingPathAndSupportsDisableAndDelete()
    {
        await using var provider = await CreateInitializedProvider();
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IFlowService>();
        var created = await service.CreateAsync("Lifecycle", CancellationToken.None);

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
        Assert.That(disabled.Disabled, Is.True);

        await service.DeleteAsync(created.Id, CancellationToken.None);
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
        services.AddFlowControlServer(configuration);
        var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider
            .GetRequiredService<IFlowControlDbContext>()
            .InitializeDatabase();
        return provider;
    }

    private static FlowNode Node(string id, string direction, string dataType) => new()
    {
        Id = id,
        Kind = "and",
        Label = id,
        Connectors =
        [
            new("connector", "Connector", direction, dataType, "right")
        ]
    };
}