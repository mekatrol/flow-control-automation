using Server.Compiler.Extensions;
using Server.Data.Context;
using Server.Services;
using Server.Services.ServiceExtensions;

namespace Tests.Unit.Flows;

public sealed class FlowDebugLeaseRepositoryTests
{
    private string _temporaryDirectory = null!;
    private string _connectionString = null!;

    [SetUp]
    public void SetUp()
    {
        _temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"flow-control-debug-lease-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_temporaryDirectory);
        _connectionString = $"Data Source={Path.Combine(_temporaryDirectory, "leases.db")};Pooling=False";
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
    public async Task EnforcesOneLeasePerFlowAndOneFlowPerContext()
    {
        await using var provider = await CreateProvider();
        var flow = await CreateFlow(provider, "Reserved flow");

        await using (var scope = provider.CreateAsyncScope())
        {
            var leases = scope.ServiceProvider.GetRequiredService<IFlowDebugLeaseRepository>();
            await leases.AcquireAsync(flow.Id, "context-one", true, CancellationToken.None);
        }

        await using var conflictScope = provider.CreateAsyncScope();
        var conflicting = conflictScope.ServiceProvider.GetRequiredService<IFlowDebugLeaseRepository>();

        var exception = Assert.ThrowsAsync<FlowDebugLeaseConflictException>(async () =>
            await conflicting.AcquireAsync(flow.Id, "context-two", true, CancellationToken.None));
        Assert.That(exception!.Code, Is.EqualTo("flow_already_being_debugged"));
    }

    [Test]
    public async Task ReleaseRequiresMatchingContextAndFlowResponseExposesSuspension()
    {
        await using var provider = await CreateProvider();
        var flow = await CreateFlow(provider, "Suspended flow");

        await using var scope = provider.CreateAsyncScope();
        var leases = scope.ServiceProvider.GetRequiredService<IFlowDebugLeaseRepository>();
        var acquired = await leases.AcquireAsync(
            flow.Id,
            "debug-context",
            true,
            CancellationToken.None);
        var flows = scope.ServiceProvider.GetRequiredService<IFlowService>();
        var visible = await flows.GetAsync(flow.Id, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(visible.TemporaryDisable?.ContextId, Is.EqualTo("debug-context"));
            Assert.That(visible.TemporaryDisable?.StartedAt, Is.Not.Empty);
            Assert.That(visible.Revision, Is.EqualTo(flow.Revision));
            Assert.That(visible.UpdatedAt, Is.EqualTo(flow.UpdatedAt));
        });
        Assert.That(
            await leases.ReleaseAsync(flow.Id, "different-context", CancellationToken.None),
            Is.False);
        Assert.That(await leases.GetByFlowAsync(flow.Id, CancellationToken.None), Is.EqualTo(acquired));
        Assert.That(
            await leases.ReleaseAsync(flow.Id, "debug-context", CancellationToken.None),
            Is.True);
    }

    [Test]
    public async Task PersistsExecutionMetadataWithoutChangingAuthoredRevision()
    {
        await using var provider = await CreateProvider();
        var flow = await CreateFlow(provider, "Executed flow");
        var completedAt = new DateTimeOffset(2026, 9, 20, 1, 2, 3, TimeSpan.Zero);

        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IFlowExecutionMetadataStore>()
            .RecordSuccessfulExecutionAsync(flow.Id, completedAt, CancellationToken.None);
        var saved = await scope.ServiceProvider.GetRequiredService<IFlowService>()
            .GetAsync(flow.Id, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(saved.LastExecutedAt, Is.EqualTo("2026-09-20T01:02:03+00:00"));
            Assert.That(saved.Revision, Is.EqualTo(flow.Revision));
            Assert.That(saved.UpdatedAt, Is.EqualTo(flow.UpdatedAt));
        });
    }

    [Test]
    public async Task StartupReconciliationReturnsAndRemovesStaleLeases()
    {
        await using var provider = await CreateProvider();
        var flow = await CreateFlow(provider, "Restarted flow");

        await using var scope = provider.CreateAsyncScope();
        var leases = scope.ServiceProvider.GetRequiredService<IFlowDebugLeaseRepository>();
        var acquired = await leases.AcquireAsync(
            flow.Id,
            "abandoned-context",
            true,
            CancellationToken.None);
        var released = await leases.ReleaseExpiredAsync(
            acquired.LastHeartbeatAt.AddTicks(1),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(released, Has.Count.EqualTo(1));
            Assert.That(released[0].ExecutionContextId, Is.EqualTo("abandoned-context"));
        });
        Assert.That(await leases.GetByFlowAsync(flow.Id, CancellationToken.None), Is.Null);
    }

    private static async Task<Flow> CreateFlow(ServiceProvider provider, string name)
    {
        await using var scope = provider.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<IFlowService>()
            .CreateAsync(name, CancellationToken.None);
    }

    private async Task<ServiceProvider> CreateProvider()
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
        services.AddServerServices(configuration);
        var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IFlowControlDbContext>()
            .InitializeDatabase();

        return provider;
    }
}