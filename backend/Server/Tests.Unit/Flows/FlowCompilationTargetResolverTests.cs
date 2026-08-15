using Server.Services;
using Server.Services.Contracts;
using Server.Services.Implementation;
using System.Text.Json;

namespace Tests.Unit.Flows;

public sealed class FlowCompilationTargetResolverTests
{
    [Test]
    public async Task ResolvesMatchingTemplateAndReferencedPointsInCanonicalOrder()
    {
        var resolver = Resolver(
            Template(),
            [Output("output-01"), Input("input-01")]);

        var target = await resolver.ResolveAsync(Source(), default);

        Assert.Multiple(() =>
        {
            Assert.That(target.ControllerTemplate.Source.Id, Is.EqualTo("controller-a"));
            Assert.That(target.Points.Select(point => point.Id),
                Is.EqualTo(new[] { "input-01", "output-01" }));
        });
    }

    [Test]
    public void RejectsAStaleTemplateRevisionBeforeReadingPoints()
    {
        var pointStore = new StubPointStore([]);
        var resolver = Resolver(Template() with { Revision = 4 }, pointStore);

        AssertDiagnostic(
            async () => await resolver.ResolveAsync(Source(), default),
            "target_mismatch",
            "/controllerTemplateRevision");
        Assert.That(pointStore.ListCallCount, Is.Zero);
    }

    [Test]
    public void RejectsAMissingReferencedPoint()
    {
        var resolver = Resolver(Template(), [Input("input-01")]);

        AssertDiagnostic(
            async () => await resolver.ResolveAsync(Source(), default),
            "missing_point",
            "/points/output-01");
    }

    [Test]
    public void RejectsAnIncompatiblePointDirection()
    {
        var resolver = Resolver(
            Template(),
            [Input("input-01"), Input("output-01")]);

        AssertDiagnostic(
            async () => await resolver.ResolveAsync(Source(), default),
            "point_direction_mismatch",
            "/points/output-01");
    }

    [Test]
    public void RejectsTargetNodeLimitsBeforeReadingPoints()
    {
        var pointStore = new StubPointStore([Input("input-01"), Output("output-01")]);
        var template = Template() with
        {
            Limits = new ControllerLimits { MaxNodesPerFlow = 1 }
        };
        var resolver = Resolver(template, pointStore);

        AssertDiagnostic(
            async () => await resolver.ResolveAsync(Source(), default),
            "limit_exceeded",
            "/nodes");
        Assert.That(pointStore.ListCallCount, Is.Zero);
    }

    private static IFlowCompilationTargetResolver Resolver(
        ControllerTemplate template,
        IReadOnlyList<Point> points) => Resolver(template, new StubPointStore(points));

    private static IFlowCompilationTargetResolver Resolver(
        ControllerTemplate template,
        StubPointStore points) => new FlowCompilationTargetResolver(
            new StubTemplateStore(template),
            new ControllerTemplateValidator(),
            points);

    private static ExecutableFlowSource Source() => new()
    {
        Id = "flow-a",
        Revision = 1,
        ControllerTemplateId = "controller-a",
        ControllerTemplateRevision = 3,
        Nodes =
        [
            Node("input-node", "digitalInput", "input-01"),
            Node("output-node", "digitalOutput", "output-01")
        ],
        Connections =
        [
            new(
                new ExecutableFlowEndpoint("input-node", "value"),
                new ExecutableFlowEndpoint("output-node", "in"))
        ]
    };

    private static ExecutableFlowNode Node(string id, string kind, string pointId)
    {
        using var document = JsonDocument.Parse($$"""{"pointId":"{{pointId}}"}""");
        return new ExecutableFlowNode
        {
            Id = id,
            Kind = kind,
            Configuration = new Dictionary<string, JsonElement>
            {
                ["pointId"] = document.RootElement.GetProperty("pointId").Clone()
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
            PointTypes = ["digital"],
            PointDirections = ["input", "output"],
            PointFeatures = ["read", "command"],
            ConnectorDataTypes = ["boolean"],
            FlowFunctions = ["read-point", "write-point"],
            ExecutionModes = ["interval"],
            RuntimeFeatures = ["bound_points"]
        }
    };

    private static Point Input(string id) => Point(id, "input", readable: true);

    private static Point Output(string id) => Point(id, "output", commandable: true);

    private static Point Point(
        string id,
        string direction,
        bool readable = false,
        bool commandable = false) => new()
        {
            Id = id,
            Name = id,
            Enabled = true,
            Implementation = "bound",
            Direction = direction,
            ValueType = "digital",
            Readable = readable,
            Commandable = commandable,
            Persistence = "volatile"
        };

    private static void AssertDiagnostic(
        AsyncTestDelegate action,
        string code,
        string path)
    {
        var exception = Assert.ThrowsAsync<FlowCompilationException>(action);
        Assert.Multiple(() =>
        {
            Assert.That(exception!.Diagnostics[0].Code, Is.EqualTo(code));
            Assert.That(exception.Diagnostics[0].Path, Is.EqualTo(path));
        });
    }

    private sealed class StubTemplateStore(ControllerTemplate template) : IControllerTemplateStore
    {
        public Task<ControllerTemplate> GetAsync(string id, CancellationToken cancellationToken) =>
            id == template.Id
                ? Task.FromResult(template)
                : throw new ControllerTemplateNotFoundException(id);

        public Task<IReadOnlyList<ControllerTemplate>> ListAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ControllerTemplate> CreateAsync(
            ControllerTemplate value,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ControllerTemplate> UpdateAsync(
            string id,
            ControllerTemplate value,
            int revision,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task DeleteAsync(
            string id,
            int revision,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class StubPointStore(IReadOnlyList<Point> points) : IPointDefinitionStore
    {
        public int ListCallCount { get; private set; }

        public Task<IReadOnlyList<Point>> ListPointsAsync(CancellationToken cancellationToken)
        {
            ListCallCount++;
            return Task.FromResult(points);
        }

        public Task<Point> GetPointAsync(string id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Point> CreatePointAsync(Point point, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Point> UpdatePointAsync(
            string id,
            Point point,
            int revision,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task DeletePointAsync(
            string id,
            int revision,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<PointGroup>> ListGroupsAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PointGroup> GetGroupAsync(string id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PointGroup> CreateGroupAsync(
            PointGroup group,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PointGroup> UpdateGroupAsync(
            string id,
            PointGroup group,
            int revision,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task DeleteGroupAsync(
            string id,
            int revision,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<Point>> MakePointsStandaloneAsync(
            string groupId,
            int groupRevision,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}