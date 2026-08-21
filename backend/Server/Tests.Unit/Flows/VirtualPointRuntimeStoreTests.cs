using Server.Common.Contracts;
using Server.Services;
using Server.Services.Contracts;
using Server.Services.Implementation;
using System.Text.Json;

namespace Tests.Unit.Flows;

[TestFixture]
public sealed class VirtualPointRuntimeStoreTests
{
    [Test]
    public async Task SharesCommittedValuesWithinAnInstanceAndIsolatesOtherInstances()
    {
        var store = new VirtualPointRuntimeStore(TimeProvider.System);
        var contract = Analog("temp-setpoint");
        await store.ActivateFlowAsync("east", "writer", [contract], new HashSet<string> { contract.Key }, default);
        await store.ActivateFlowAsync("east", "reader", [contract], new HashSet<string>(), default);
        await store.ActivateFlowAsync("west", "writer", [contract], new HashSet<string> { contract.Key }, default);

        await store.CommitAsync("east", "writer", [new FlowVmCommand(contract.Key, FlowVmValue.FromNumber(21.5))], default);

        Assert.Multiple(() =>
        {
            Assert.That(store.TrySnapshot("east", contract.Key, out var east), Is.True);
            Assert.That(east.Value?.Number, Is.EqualTo(21.5));
            Assert.That(east.Version, Is.EqualTo(1));
            Assert.That(store.TrySnapshot("west", contract.Key, out var west), Is.True);
            Assert.That(west.Value, Is.Null);
            Assert.That(west.Version, Is.Zero);
        });
    }

    [Test]
    public async Task EnforcesOneWriterAndAtomicallyCommitsTypedValues()
    {
        var store = new VirtualPointRuntimeStore(TimeProvider.System);
        var analog = Analog("analog");
        var digital = Digital("digital");
        await store.ActivateFlowAsync("server", "owner", [analog, digital], new HashSet<string> { analog.Key, digital.Key }, default);

        Assert.ThrowsAsync<VirtualPointWriterConflictException>(() =>
            store.ActivateFlowAsync("server", "other", [analog], new HashSet<string> { analog.Key }, default));
        Assert.ThrowsAsync<InvalidOperationException>(() => store.CommitAsync(
            "server", "owner", [new FlowVmCommand(analog.Key, true)], default));
        Assert.That(store.TrySnapshot("server", analog.Key, out var unchanged), Is.True);
        Assert.That(unchanged.Value, Is.Null);

        await store.CommitAsync("server", "owner",
            [new FlowVmCommand(analog.Key, FlowVmValue.FromNumber(3.25)), new FlowVmCommand(digital.Key, true)], default);
        Assert.Multiple(() =>
        {
            Assert.That(store.List("server"), Has.Count.EqualTo(2));
            Assert.That(store.List("server"), Has.All.Property(nameof(VirtualPointRuntimeValue.Version)).EqualTo(1));
        });
    }

    [Test]
    public async Task UsesTypedDefaultUntilFirstCommitAndReleasesOwnership()
    {
        var store = new VirtualPointRuntimeStore(TimeProvider.System);
        var contract = Analog("defaulted") with { RelinquishDefault = JsonSerializer.SerializeToElement(18.0) };
        await store.ActivateFlowAsync("server", "first", [contract], new HashSet<string> { contract.Key }, default);
        Assert.That(store.TrySnapshot("server", contract.Key, out var initial), Is.True);
        Assert.That(initial.Value?.Number, Is.EqualTo(18.0));
        Assert.That(initial.Quality, Is.EqualTo(DataQuality.Good));

        store.ReleaseFlow("server", "first");
        Assert.DoesNotThrowAsync(() => store.ActivateFlowAsync("server", "second", [contract], new HashSet<string> { contract.Key }, default));
    }

    [Test]
    public async Task RestoresRetainedValuesAcrossRuntimeStoreRecreation()
    {
        var persistence = new RetainedStore();
        var contract = Analog("retained") with { Persistence = VirtualPointPersistence.Retained };
        var first = new VirtualPointRuntimeStore(TimeProvider.System, persistence);
        await first.ActivateFlowAsync("east", "writer", [contract], new HashSet<string> { contract.Key }, default);
        await first.CommitAsync("east", "writer", [new FlowVmCommand(contract.Key, FlowVmValue.FromNumber(19.75))], default);

        var restored = new VirtualPointRuntimeStore(TimeProvider.System, persistence);
        await restored.ActivateFlowAsync("east", "reader", [contract], new HashSet<string>(), default);

        Assert.Multiple(() =>
        {
            Assert.That(restored.TrySnapshot("east", contract.Key, out var value), Is.True);
            Assert.That(value.Value?.Number, Is.EqualTo(19.75));
            Assert.That(value.Quality, Is.EqualTo(DataQuality.Good));
            Assert.That(value.Version, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ReleasesTheLastVolatileAllocationWithItsOwningFlow()
    {
        var store = new VirtualPointRuntimeStore(TimeProvider.System);
        var contract = Digital("pulse");
        await store.ActivateFlowAsync("controller", "owner", [contract], new HashSet<string> { contract.Key }, default);
        await store.CommitAsync("controller", "owner", [new FlowVmCommand(contract.Key, true)], default);

        store.ReleaseFlow("controller", "owner");
        await store.ActivateFlowAsync("controller", "replacement", [contract], new HashSet<string>(), default);

        Assert.That(store.TrySnapshot("controller", contract.Key, out var value), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(value.Value, Is.Null);
            Assert.That(value.Quality, Is.EqualTo(DataQuality.Unavailable));
            Assert.That(value.Version, Is.Zero);
        });
    }

    [Test]
    public async Task ConcurrentCommitsPublishOnlyCompleteMonotonicRecords()
    {
        var store = new VirtualPointRuntimeStore(TimeProvider.System);
        var analog = Analog("level");
        var digital = Digital("enabled");
        await store.ActivateFlowAsync("server", "writer", [analog, digital], new HashSet<string> { analog.Key, digital.Key }, default);
        await store.ActivateFlowAsync("server", "reader", [analog, digital], new HashSet<string>(), default);

        await Task.WhenAll(Enumerable.Range(1, 100).Select(index => store.CommitAsync(
            "server",
            "writer",
            [new FlowVmCommand(analog.Key, FlowVmValue.FromNumber(index)), new FlowVmCommand(digital.Key, index % 2 == 0)],
            default)));

        var values = store.List("server");
        Assert.Multiple(() =>
        {
            Assert.That(values, Has.Count.EqualTo(2));
            Assert.That(values, Has.All.Property(nameof(VirtualPointRuntimeValue.Version)).EqualTo(100));
            Assert.That(values, Has.All.Property(nameof(VirtualPointRuntimeValue.Quality)).EqualTo(DataQuality.Good));
            Assert.That(values.Select(item => item.Timestamp), Has.All.Not.Null.And.Not.Empty);
        });
    }

    private static VirtualPointDeclaration Analog(string key) => new()
    {
        Key = key,
        ValueType = FlowPointValueType.Analog,
        Readable = true,
        Commandable = true,
        Persistence = VirtualPointPersistence.Volatile
    };

    private static VirtualPointDeclaration Digital(string key) => Analog(key) with { ValueType = FlowPointValueType.Digital };

    private sealed class RetainedStore : IVirtualPointRetainedStore
    {
        private readonly Dictionary<(string Instance, string Point), RetainedVirtualPointValue> _values = [];

        public Task<RetainedVirtualPointValue?> ReadAsync(string executionInstanceId, string pointKey, CancellationToken cancellationToken) =>
            Task.FromResult(_values.GetValueOrDefault((executionInstanceId, pointKey)));

        public Task WriteAsync(string executionInstanceId, IReadOnlyDictionary<string, RetainedVirtualPointValue> values, CancellationToken cancellationToken)
        {
            foreach (var (pointKey, value) in values) _values[(executionInstanceId, pointKey)] = value;
            return Task.CompletedTask;
        }
    }
}
