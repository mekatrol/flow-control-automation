using Server.Common;
using Server.Common.Contracts;
using Server.Common.Services;
using Server.Compiler;
using Server.Compiler.Contracts;
using Server.Compiler.Extensions;
using Server.Compiler.Services;
using Server.Services.Implementation;
using System.Text.Json;

namespace Tests.Unit.Flows;

public sealed class FlowCompilationTargetResolverTests
{
    [Test]
    public async Task ResolvesMatchingTemplateAndReferencedPointsInCanonicalOrder()
    {
        using var context = Resolver(
            Template(),
            [Output("output-01"), Input("input-01")]);

        var target = await context.Resolver.ResolveAsync(Source(), default);

        Assert.Multiple(() =>
        {
            Assert.That(target.ControllerTemplate.Source.Id, Is.EqualTo("controller-a"));
            Assert.That(
                target.Points.Select(point => point.Id),
                Is.EqualTo(new[] { "input-01", "output-01" }));
        });
    }

    [Test]
    public async Task ResolvesReadableAndCommandableVirtualValuesAsFlowIo()
    {
        using var context = Resolver(
            Template(),
            [
                VirtualValue("input-01", readable: true),
                VirtualValue("output-01", commandable: true)
            ]);

        var target = await context.Resolver.ResolveAsync(Source(), default);

        Assert.That(
            target.Points.Select(point => point.Id),
            Is.EqualTo(new[] { "input-01", "output-01" }));
    }

    [Test]
    public void RejectsAStaleTemplateRevisionBeforeReadingPoints()
    {
        var pointStore = new StubPointStore([]);

        using var context = Resolver(
            Template() with { Revision = 4 },
            pointStore);

        AssertDiagnostic(
            async () => await context.Resolver.ResolveAsync(Source(), default),
            FlowCompilationDiagnosticCode.ControllerTemplateRevisionMismatch,
            "/controllerTemplateRevision");

        Assert.That(pointStore.ListCallCount, Is.Zero);
    }

    [Test]
    public void RejectsAMissingReferencedPoint()
    {
        using var context = Resolver(
            Template(),
            [Input("input-01")]);

        AssertDiagnostic(
            async () => await context.Resolver.ResolveAsync(Source(), default),
            FlowCompilationDiagnosticCode.MissingPoint,
            "/points/output-01");
    }

    [Test]
    public void RejectsAnIncompatiblePointDirection()
    {
        using var context = Resolver(
            Template(),
            [Input("input-01"), Input("output-01")]);

        AssertDiagnostic(
            async () => await context.Resolver.ResolveAsync(Source(), default),
            FlowCompilationDiagnosticCode.PointDirectionMismatch,
            "/points/output-01");
    }

    [Test]
    public void RejectsTargetNodeLimitsBeforeReadingPoints()
    {
        var pointStore = new StubPointStore(
            [Input("input-01"), Output("output-01")]);

        var template = Template() with
        {
            Limits = new ControllerLimits
            {
                MaxNodesPerFlow = 1
            }
        };

        using var context = Resolver(template, pointStore);

        AssertDiagnostic(
            async () => await context.Resolver.ResolveAsync(Source(), default),
            FlowCompilationDiagnosticCode.TargetNodeLimitExceeded,
            "/nodes");

        Assert.That(pointStore.ListCallCount, Is.Zero);
    }

    private static ResolverContext Resolver(
        ControllerTemplate template,
        IReadOnlyList<FlowPoint> points) =>
        Resolver(template, new StubPointStore(points));

    private static ResolverContext Resolver(
        ControllerTemplate template,
        StubPointStore points)
    {
        var services = new ServiceCollection();

        services.AddFlowCompilerServices();

        services.AddSingleton<IControllerTemplateStore>(
            new StubTemplateStore(template));

        services.AddSingleton<IControllerTemplateValidator,
            ControllerTemplateValidator>();

        services.AddSingleton<IPointDefinitionStore>(points);

        return new ResolverContext(services.BuildServiceProvider());
    }

    private static ExecutableFlowSource Source() => new()
    {
        Id = "flow-a",
        Revision = 1,
        ControllerTemplateId = "controller-a",
        ControllerTemplateRevision = 3,
        Nodes =
        [
            Node("input-node", FlowNodeKind.DigitalInput, "input-01"),
            Node("output-node", FlowNodeKind.DigitalOutput, "output-01")
        ],
        Connections =
        [
            new(
                new ExecutableFlowEndpoint("input-node", "value"),
                new ExecutableFlowEndpoint("output-node", "in"))
        ]
    };

    private static ExecutableFlowNode Node(
        string id,
        FlowNodeKind kind,
        string pointId)
    {
        using var document =
            JsonDocument.Parse($$"""{"pointId":"{{pointId}}"}""");

        return new ExecutableFlowNode
        {
            Id = id,
            Kind = kind,
            Configuration = new Dictionary<string, JsonElement>
            {
                ["pointId"] = document.RootElement
                    .GetProperty("pointId")
                    .Clone()
            }
        };
    }

    private static ControllerTemplate Template() => new()
    {
        Id = "controller-a",
        Name = "Controller A",
        Revision = 3,
        Capabilities = new ControllerCapabilities
        {
            PointTypes =
            [
                FlowPointValueType.Digital
            ],
            PointDirections =
            [
                DataDirection.Input,
                DataDirection.Output
            ],
            PointFeatures =
            [
                ControllerPointFeature.Read,
                ControllerPointFeature.Command
            ],
            ConnectorDataTypes =
            [
                ConnectorDataType.Boolean
            ],
            FlowFunctions =
            [
                FlowFunctionKind.ReadPoint,
                FlowFunctionKind.WritePoint
            ],
            ExecutionModes =
            [
                ExecutionMode.Interval
            ],
            RuntimeFeatures =
            [
                ControllerRuntimeFeature.BoundPoints
            ]
        }
    };

    private static FlowPoint Input(string id) =>
        Point(
            id,
            DataDirection.Input,
            readable: true);

    private static FlowPoint Output(string id) =>
        Point(
            id,
            DataDirection.Output,
            commandable: true);

    private static FlowPoint VirtualValue(
        string id,
        bool readable = false,
        bool commandable = false) =>
        Point(
            id,
            DataDirection.Value,
            readable,
            commandable) with
        {
            Implementation = "virtual"
        };

    private static FlowPoint Point(
        string id,
        DataDirection direction,
        bool readable = false,
        bool commandable = false) => new()
        {
            Id = id,
            Name = id,
            Enabled = true,
            Implementation = "bound",
            Direction = direction,
            ValueType = FlowPointValueType.Digital,
            Readable = readable,
            Commandable = commandable,
            Persistence = "volatile"
        };

    private static void AssertDiagnostic(
        AsyncTestDelegate action,
        FlowCompilationDiagnosticCode code,
        string path)
    {
        var exception =
            Assert.ThrowsAsync<FlowCompilationException>(action);

        Assert.Multiple(() =>
        {
            Assert.That(
                exception!.Diagnostics[0].Code,
                Is.EqualTo(code));

            Assert.That(
                exception.Diagnostics[0].Path,
                Is.EqualTo(path));
        });
    }

    private sealed class ResolverContext : IDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly IServiceScope _scope;

        public ResolverContext(ServiceProvider provider)
        {
            _provider = provider;
            _scope = provider.CreateScope();

            Resolver = _scope.ServiceProvider
                .GetRequiredService<IFlowCompilationTargetResolver>();
        }

        public IFlowCompilationTargetResolver Resolver { get; }

        public void Dispose()
        {
            _scope.Dispose();
            _provider.Dispose();
        }
    }

    private sealed class StubTemplateStore(
        ControllerTemplate template) : IControllerTemplateStore
    {
        public Task<ControllerTemplate> GetAsync(
            string id,
            CancellationToken cancellationToken) =>
            id == template.Id
                ? Task.FromResult(template)
                : throw new ControllerTemplateNotFoundException(id);

        public Task<IReadOnlyList<ControllerTemplate>> ListAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ControllerTemplate> CreateAsync(
            ControllerTemplate value,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ControllerTemplate> UpdateAsync(
            string id,
            ControllerTemplate value,
            int revision,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task DeleteAsync(
            string id,
            int revision,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class StubPointStore(
        IReadOnlyList<FlowPoint> points) : IPointDefinitionStore
    {
        public int ListCallCount { get; private set; }

        public Task<IReadOnlyList<FlowPoint>> ListPointsAsync(
            CancellationToken cancellationToken)
        {
            ListCallCount++;
            return Task.FromResult(points);
        }

        public Task<FlowPoint> GetPointAsync(
            string id,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<FlowPoint> CreatePointAsync(
            FlowPoint point,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<FlowPoint> UpdatePointAsync(
            string id,
            FlowPoint point,
            int revision,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task DeletePointAsync(
            string id,
            int revision,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<PointGroup>> ListGroupsAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PointGroup> GetGroupAsync(
            string id,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PointGroup> CreateGroupAsync(
            PointGroup group,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PointGroup> UpdateGroupAsync(
            string id,
            PointGroup group,
            int revision,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task DeleteGroupAsync(
            string id,
            int revision,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<FlowPoint>> MakePointsStandaloneAsync(
            string groupId,
            int groupRevision,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}