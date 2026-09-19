using Tests.Unit.Helpers;

namespace Tests.Unit.FlowExecution.Runtime;

public sealed class ServerFlowPointAdapterTests
{
    [Test]
    public async Task PublishesExternalCommandsThroughConfiguredPointMappings()
    {
        var point = new AutomationPoint
        {
            Id = "intensity",
            Name = "Intensity",
            Enabled = true,
            Direction = DataDirectionType.Output,
            ValueType = AutomationPointValueType.Analog,
            Commandable = true,
            Persistence = "volatile",
            Mapping = "outputs/intensity"
        };
        var source = new PointSource
        {
            Id = "lights",
            Name = "Lights",
            Enabled = true,
            Kind = PointSourceKind.Http,
            Mappings = [new PointMapping { Id = "outputs", Aliases = ["intensity"] }],
            Points = [point]
        };
        var execution = new MappingExecution();
        await using var provider = TestServices.CreateProvider(services =>
        {
            services.Replace(ServiceDescriptor.Scoped<IPointDefinitionReader>(
                _ => new PointDefinitions(point)));
            services.Replace(ServiceDescriptor.Scoped<IPointSourceService>(
                _ => new PointSources(source)));
            services.Replace(ServiceDescriptor.Scoped<IPointMappingExecutionService>(_ => execution));
        });
        var adapter = provider.GetRequiredService<IFlowPointAdapter>();

        await adapter.PublishAsync(
            "lighting-flow",
            [new FlowVmCommand("lights/intensity", FlowVmValue.FromNumber(25))],
            default);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(execution.Resolution?.Source.Id, Is.EqualTo("lights"));
            Assert.That(execution.Resolution?.Point.Id, Is.EqualTo("intensity"));
            Assert.That(execution.Value, Is.EqualTo(25D));
        }
    }

    private sealed class PointDefinitions(AutomationPoint point) : IPointDefinitionReader
    {
        public Task<IReadOnlyList<AutomationPoint>> ListPointsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AutomationPoint>>([point]);

        public Task<AutomationPoint> GetPointAsync(
            string sourceId,
            string pointId,
            CancellationToken cancellationToken) => Task.FromResult(point);
    }

    private sealed class PointSources(PointSource source) : IPointSourceService
    {
        public Task<PointSource> GetAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(source);

        public Task<PaginatedResult<PointSource>> ListAsync(
            PointSourceListOptions options,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PointSource> CreateAsync(PointSource value, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PointSource> UpdateAsync(
            string id,
            PointSource value,
            int revision,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task DeleteAsync(string id, int revision, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class MappingExecution : IPointMappingExecutionService
    {
        public PointMappingResolution? Resolution { get; private set; }
        public object? Value { get; private set; }

        public Task<PointMappingCommandResult> CommandAsync(
            PointMappingResolution resolution,
            object? value,
            CancellationToken cancellationToken)
        {
            Resolution = resolution;
            Value = value;

            return Task.FromResult(new PointMappingCommandResult(string.Empty, DateTimeOffset.UtcNow));
        }

        public Task<PointMappingReadResult> ReadAsync(
            PointMappingResolution resolution,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PointMappingReadResult> ReadMappingAsync(
            PointSource source,
            PointMapping mapping,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PointMappingCommandResult> CommandMappingAsync(
            PointSource source,
            PointMapping mapping,
            string payload,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}