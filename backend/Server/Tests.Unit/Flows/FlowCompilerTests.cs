using Server.Common.Contracts;
using Server.Compiler;
using Server.Compiler.Contracts;
using Server.Compiler.Extensions;
using Server.Compiler.Services;
using Server.Services.Contracts;
using System.Text;
using System.Text.Json;
using Tests.Unit.Helpers;

namespace Tests.Unit.Flows;

public sealed class FlowCompilerTests
{
    private ServiceProvider _serviceProvider = null!;
    private IFlowCompiler _compiler = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var services = new ServiceCollection();
        services.AddFlowCompilerServices();

        _serviceProvider = services.BuildServiceProvider();
        _compiler = _serviceProvider.GetRequiredService<IFlowCompiler>();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _serviceProvider.Dispose();
    }

    private static readonly FlowNodeKind[] FlowFunctionKinds =
    [
        FlowNodeKind.Add, FlowNodeKind.AnalogInput, FlowNodeKind.AnalogOutput, FlowNodeKind.And, FlowNodeKind.Average, FlowNodeKind.Calculator, FlowNodeKind.Calendar,
        FlowNodeKind.Clamp, FlowNodeKind.Comparator, FlowNodeKind.Delay, FlowNodeKind.DigitalConstant, FlowNodeKind.DigitalInput, FlowNodeKind.DigitalOutput,
        FlowNodeKind.FlowInput, FlowNodeKind.FlowOutput, FlowNodeKind.If, FlowNodeKind.Line, FlowNodeKind.LevelShifter, FlowNodeKind.Max, FlowNodeKind.Memory, FlowNodeKind.Min,
        FlowNodeKind.Nand, FlowNodeKind.Nor, FlowNodeKind.Not, FlowNodeKind.NumericConstant, FlowNodeKind.OnDelay, FlowNodeKind.Or, FlowNodeKind.Override, FlowNodeKind.Pulse,
        FlowNodeKind.QualityGood, FlowNodeKind.RisingEdge, FlowNodeKind.Schedule, FlowNodeKind.Selector, FlowNodeKind.Sequence, FlowNodeKind.Split ,FlowNodeKind.Timer,
        FlowNodeKind.Xnor, FlowNodeKind.Xor
    ];

    private static readonly string FixtureSourceRoot = Path.Combine(
        AppContext.BaseDirectory,
        "ContractFixtures",
        "flow-il-v1");

    private static readonly string FixtureExpectedRoot = Path.Combine(
        AppContext.BaseDirectory,
        "ContractFixtures",
        "flow-il-v1");

    private static readonly string FixtureUpdateRoot = Path.Combine(
        FixtureUpdater.WorkspaceDirectory(),
        "testdata",
        "contracts",
        "flow-il-v1");

    private static ExecutableFlowSource GetSourceFromKind(FlowNodeKind kind)
    {
        var numericInputs = kind switch
        {
            FlowNodeKind.Add or FlowNodeKind.Comparator or FlowNodeKind.Min or FlowNodeKind.Max or FlowNodeKind.Sequence => ["a", "b"],
            FlowNodeKind.Selector => ["a", "b"],
            FlowNodeKind.Average or FlowNodeKind.Calculator or FlowNodeKind.Clamp or FlowNodeKind.Line or FlowNodeKind.Split => new[] { "input" },
            FlowNodeKind.LevelShifter => ["in"],
            FlowNodeKind.AnalogOutput => ["in"],
            FlowNodeKind.Memory or FlowNodeKind.QualityGood => ["in"],
            _ => []
        };

        var booleanInputs = kind switch
        {
            FlowNodeKind.Not or FlowNodeKind.OnDelay or FlowNodeKind.RisingEdge => ["in"],
            FlowNodeKind.And or FlowNodeKind.Or or FlowNodeKind.Nand or FlowNodeKind.Nor or FlowNodeKind.Xnor or FlowNodeKind.Xor => ["a", "b"],
            FlowNodeKind.If => ["condition", "whenTrue", "whenFalse"],
            FlowNodeKind.Selector => ["condition"],
            FlowNodeKind.Override or FlowNodeKind.Delay or FlowNodeKind.Timer or FlowNodeKind.Pulse => ["input"],
            FlowNodeKind.DigitalOutput => ["in"],
            FlowNodeKind.FlowOutput => new[] { "value" },
            _ => []
        };

        var configuration = kind switch
        {
            FlowNodeKind.DigitalInput or FlowNodeKind.DigitalOutput or FlowNodeKind.AnalogInput or FlowNodeKind.AnalogOutput =>
                Config("pointId", "test-point"),
            FlowNodeKind.DigitalConstant =>
                Config("value", true),
            FlowNodeKind.NumericConstant or FlowNodeKind.Memory =>
                Config("value", 1D),
            FlowNodeKind.Comparator =>
                Config("operator", "gt"),
            FlowNodeKind.LevelShifter or FlowNodeKind.Line =>
                Config(("gain", 1D), ("offset", 0D)),
            FlowNodeKind.OnDelay or FlowNodeKind.Delay or FlowNodeKind.Timer =>
                Config("durationMs", 100D),
            FlowNodeKind.Clamp => Config(("minimum", 0D), ("maximum", 100D)),
            FlowNodeKind.Schedule or FlowNodeKind.Calendar =>
                Config("enabled", true),
            FlowNodeKind.FlowInput or FlowNodeKind.FlowOutput =>
                Config("interfaceId", kind == FlowNodeKind.FlowInput ? "test-input" : "test-output"),
            _ => []
        };

        var nodes = new List<ExecutableFlowNode>();
        var connections = new List<ExecutableFlowConnection>();

        foreach (var port in numericInputs)
        {
            var id = $"number-{port}";
            nodes.Add(new ExecutableFlowNode { Id = id, Kind = FlowNodeKind.NumericConstant, Configuration = Config("value", 1D) });
            connections.Add(new ExecutableFlowConnection(new ExecutableFlowEndpoint(id, "value"), new ExecutableFlowEndpoint("test-node", port)));
        }

        foreach (var port in booleanInputs)
        {
            var id = $"boolean-{port}";
            nodes.Add(new ExecutableFlowNode { Id = id, Kind = FlowNodeKind.DigitalConstant, Configuration = Config("value", true) });
            connections.Add(new ExecutableFlowConnection(new ExecutableFlowEndpoint(id, "value"), new ExecutableFlowEndpoint("test-node", port)));
        }

        nodes.Add(new ExecutableFlowNode { Id = "test-node", Kind = kind, Configuration = configuration });

        return new ExecutableFlowSource
        {
            Id = $"test-{kind}",
            Revision = 1,
            ControllerTemplateId = "fixture",
            ControllerTemplateRevision = 1,
            Nodes = nodes,
            Connections = connections,
            Interface = new FlowInterface
            {
                Inputs = [new FlowInterfaceInput { Id = "test-input", Name = "Test input", DataType = DataType.Boolean, Required = false }],
                Outputs = [new FlowInterfaceOutput { Id = "test-output", Name = "Test output", DataType = DataType.Boolean }]
            }
        };
    }

    /// <summary>
    /// Purpose: Ensures every executable function has a compiler-valid ordinary flow fixture.
    /// Description: Builds a minimal fully-driven graph for each canonical kind and compiles it through Flow IL.
    /// </summary>
    [TestCaseSource(nameof(FlowFunctionKinds))]
    public void EveryExecutableTutorialKindCompilesThroughTheNormalCompiler(FlowNodeKind kind)
    {
        // Arrange: Create a current-schema fixture with typed constant drivers and canonical configuration.
        var source = GetSourceFromKind(kind);

        // Act: Compile through the production compiler rather than test-specific semantics.
        var compilationRequest = BuildCompilationRequest(source);
        var compilation = _compiler.Compile(compilationRequest);

        // Assert: A bounded current artifact and stable source identity are produced.
        Assert.Multiple(() =>
        {
            Assert.That(compilation.Artifact.Length, Is.GreaterThan(0));
            Assert.That(compilation.Artifact.Length, Is.LessThanOrEqualTo(16_384));
            Assert.That(compilation.NodeIndices, Does.ContainKey("test-node"));
        });
    }

    [TestCase("valid-two-button-and")]
    [TestCase("valid-source-order-permutation")]
    [TestCase("valid-memory-feedback")]
    public void CompilesGoldenSourceToTheExactCanonicalArtifact(string fixture)
    {
        var result = CompileFixture(fixture);

        var sourceRoot = FixtureUpdater.IsEnabled(fixture)
            ? FixtureUpdateRoot
            : FixtureExpectedRoot;

        var expected = File.ReadAllBytes(
            Path.Combine(sourceRoot, fixture, "artifact.bin"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Artifact.ToArray(), Is.EqualTo(expected));
            Assert.That(result.ArtifactSha256,
                Is.EqualTo(Convert.ToHexStringLower(
                    System.Security.Cryptography.SHA256.HashData(expected))));
            Assert.That(result.ArtifactVersion, Is.EqualTo(1));
            Assert.That(result.NodeIndices.Keys, Is.EqualTo(result.Schedule));
        });
    }

    [TestCase(FlowNodeKind.Nand, FlowOpcode.Nand)]
    [TestCase(FlowNodeKind.Nor, FlowOpcode.Nor)]
    [TestCase(FlowNodeKind.Xor, FlowOpcode.Xor)]
    [TestCase(FlowNodeKind.Xnor, FlowOpcode.Xnor)]
    public void LowersExpandedBooleanNodesToTheirNormativeOpcode(FlowNodeKind kind, FlowOpcode opcode)
    {
        const string fixture = "valid-two-button-and";

        var source = ReadSource(fixture);
        source = source with
        {
            Nodes = [.. source.Nodes.Select(node =>
            node.Kind == FlowNodeKind.And
                ? node with { Kind = kind }
                : node)]
        };

        var compilationRequest = BuildCompilationRequest(source);
        var artifact = _compiler
            .Compile(compilationRequest)
            .Artifact
            .ToArray();

        var instructionSection =
            System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(
                artifact.AsSpan(128 + (3 * 48) + 4, 4));

        var instructionCount =
            System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(
                artifact.AsSpan(32, 4));

        Assert.That(
            Enumerable.Range(0, checked((int)instructionCount))
                .Select(index =>
                    artifact[checked((int)instructionSection) + (index * 12)]),
            Does.Contain((byte)opcode));
    }

    [Test]
    public void RejectsUnsupportedNodesWithAStableGraphPath()
    {
        var source = ReadSource("valid-two-button-and") with
        {
            Nodes =
            [
                ReadSource("valid-two-button-and").Nodes[0] with { Kind = FlowNodeKind.Unknown }
            ],
            Connections = []
        };

        AssertDiagnostic(
            () => _compiler.Compile(BuildCompilationRequest(source)),
            FlowCompilationDiagnosticCode.UnsupportedNode,
            "/nodes/0/kind");
    }

    [Test]
    public void RejectsCombinationalCyclesWithTheLexicallyFirstNodePath()
    {
        var source = ReadSource("valid-two-button-and") with
        {
            Nodes =
            [
                new ExecutableFlowNode { Id = "not-a", Kind = FlowNodeKind.Not },
                new ExecutableFlowNode { Id = "not-b", Kind = FlowNodeKind.Not }
            ],
            Connections =
            [
                new ExecutableFlowConnection(new ExecutableFlowEndpoint("not-a", "value"), new ExecutableFlowEndpoint("not-b", "in")),
                new ExecutableFlowConnection(new ExecutableFlowEndpoint("not-b", "value"), new ExecutableFlowEndpoint("not-a", "in"))
            ]
        };

        AssertDiagnostic(
            () => _compiler.Compile(BuildCompilationRequest(source)),
            FlowCompilationDiagnosticCode.CombinationalCycle,
            "/nodes/not-a");
    }

    [Test]
    public void RejectsMissingInputDriversBeforeEncoding()
    {
        var source = ReadSource("valid-two-button-and") with { Connections = [] };

        AssertDiagnostic(
            () => _compiler.Compile(BuildCompilationRequest(source)),
            FlowCompilationDiagnosticCode.MissingInputDriver,
            "/nodes/and-main/ports/a");
    }

    [Test]
    public void ReportsScheduledResourceRequirementsForThePlcScan()
    {
        var result = CompileFixture("valid-memory-feedback");

        Assert.Multiple(() =>
        {
            Assert.That(result.Schedule, Is.EqualTo(new[]
            {
                "constant-2",
                "memory-1",
                "output-01-node"
            }));
            Assert.That(result.MaximumWorkPerScan, Is.EqualTo(5));
            Assert.That(result.WorkingBytes, Is.EqualTo(128));
            Assert.That(result.MaximumSnapshotBytes, Is.EqualTo(16384));
        });
    }

    [TestCase(0)]
    [TestCase(99)]
    public void RejectsEveryNonCurrentArtifactVersionWithAStablePath(int artifactVersion)
    {
        var request = BuildCompilationRequest(ReadSource("valid-two-button-and")) with { ArtifactVersion = artifactVersion };

        AssertDiagnostic(
            () => _compiler.Compile(request),
            FlowCompilationDiagnosticCode.UnsupportedArtifactVersion,
            "/artifactVersion");
    }

    [Test]
    public void CapturesResolvedPointRevisionsInTheCanonicalArtifact()
    {
        var source = ReadSource("valid-two-button-and");
        var firstRequest = BuildCompilationRequest(source);
        var first = _compiler.Compile(firstRequest);
        var changedPoint = firstRequest.Target.Points[0] with { Revision = 2 };
        var second = _compiler.Compile(firstRequest with
        {
            Target = firstRequest.Target with
            {
                Points = [changedPoint, .. firstRequest.Target.Points.Skip(1)]
            }
        });

        Assert.That(second.Artifact.ToArray(), Is.Not.EqualTo(first.Artifact.ToArray()));
    }

    [Test]
    public void InterfaceTerminalsCompileDeterministicallyWithStableSourceIdentity()
    {
        var source = ReadSource("valid-two-button-and") with
        {
            Interface = new FlowInterface
            {
                Inputs = [new FlowInterfaceInput { Id = "temperature", Name = "Temperature", DataType = DataType.Number, Units = "°C", Required = true }],
                Outputs = [new FlowInterfaceOutput { Id = "result", Name = "Result", DataType = DataType.Number, Units = "°C" }]
            },
            Nodes =
            [
                new ExecutableFlowNode { Id = "input", Kind = FlowNodeKind.FlowInput, Configuration = new Dictionary<string, JsonElement> { ["interfaceId"] = JsonSerializer.SerializeToElement("temperature") } },
                new ExecutableFlowNode { Id = "output", Kind = FlowNodeKind.FlowOutput, Configuration = new Dictionary<string, JsonElement> { ["interfaceId"] = JsonSerializer.SerializeToElement("result") } }
            ],
            Connections = [new ExecutableFlowConnection(new ExecutableFlowEndpoint("input", "value"), new ExecutableFlowEndpoint("output", "value"))]
        };
        var request = BuildCompilationRequest(source);

        var first = _compiler.Compile(request);
        var second = _compiler.Compile(request);

        Assert.Multiple(() =>
        {
            Assert.That(first.Artifact.ToArray(), Is.EqualTo(second.Artifact.ToArray()));
            Assert.That(first.Schedule, Is.EqualTo(new[] { "input", "output" }));
            Assert.That(first.NodeIndices.Keys, Is.EquivalentTo(["input", "output"]));
            Assert.That(Encoding.UTF8.GetString(first.Artifact.Span), Does.Contain("temperature"));
            Assert.That(Encoding.UTF8.GetString(first.Artifact.Span), Does.Contain("result"));
        });
    }

    [Test]
    public void RejectsAnUnresolvedPointDependencyBeforeEmission()
    {
        var source = ReadSource("valid-two-button-and");
        var request = BuildCompilationRequest(source);
        request = request with { Target = request.Target with { Points = [.. request.Target.Points.Skip(1)] } };

        AssertDiagnostic(
            () => _compiler.Compile(request),
            FlowCompilationDiagnosticCode.MissingPoint,
            $"/points/{source.Nodes[0].Configuration["pointId"].GetString()}");
    }
    private static Dictionary<string, JsonElement> Config(string key, object value) =>
        new() { [key] = JsonSerializer.SerializeToElement(value) };

    private static Dictionary<string, JsonElement> Config(params (string Key, object Value)[] values) =>
        values.ToDictionary(value => value.Key, value => JsonSerializer.SerializeToElement(value.Value), StringComparer.Ordinal);

    private static ExecutableFlowSource ReadSource(string fixture)
    {
        var sourceRoot = FixtureUpdater.IsEnabled(fixture)
          ? FixtureUpdateRoot
          : FixtureSourceRoot;

        var json = File.ReadAllText(Path.Combine(sourceRoot, fixture, "source-flow.json"));
        return JsonSerializer.Deserialize<ExecutableFlowSource>(json, FlowControlJson.Options)!;
    }

    private static FlowCompilationRequest BuildCompilationRequest(ExecutableFlowSource source) => new()
    {
        Source = source,
        Target = new FlowCompilationTarget
        {
            ControllerTemplate = new ValidatedControllerTemplate(
                new ControllerTemplate
                {
                    Id = source.ControllerTemplateId,
                    Name = "Fixture target",
                    Revision = checked((int)source.ControllerTemplateRevision)
                },
                new HashSet<FlowPointValueType> { FlowPointValueType.Digital, FlowPointValueType.Analog },
                new HashSet<DataDirection> { DataDirection.Input, DataDirection.Output },
                new HashSet<ControllerPointFeature>(),
                new HashSet<ConnectorDataType> { ConnectorDataType.Boolean, ConnectorDataType.Number },
                new HashSet<FlowFunctionKind>(),
                new HashSet<ExecutionMode>(),
                new HashSet<ControllerRuntimeFeature>()),
            Points = [.. source.Nodes
                .Where(node => node.Kind is FlowNodeKind.DigitalInput or FlowNodeKind.DigitalOutput or FlowNodeKind.AnalogInput or FlowNodeKind.AnalogOutput)
                .Select(node => new FlowPoint
                {
                    Id = node.Configuration["pointId"].GetString()!,
                    Name = node.Configuration["pointId"].GetString()!,
                    Enabled = true,
                    Implementation = "virtual",
                    Direction = node.Kind is FlowNodeKind.DigitalInput or FlowNodeKind.AnalogInput ? DataDirection.Input : DataDirection.Output,
                    ValueType = node.Kind is FlowNodeKind.AnalogInput or FlowNodeKind.AnalogOutput ? FlowPointValueType.Analog : FlowPointValueType.Digital,
                    Readable = node.Kind is FlowNodeKind.DigitalInput or FlowNodeKind.AnalogInput,
                    Commandable = node.Kind is FlowNodeKind.DigitalOutput or FlowNodeKind.AnalogOutput,
                    Persistence = "volatile",
                    Revision = 1
                })
                .DistinctBy(point => point.Id, StringComparer.Ordinal)]
        }
    };

    private static void AssertDiagnostic(TestDelegate action, FlowCompilationDiagnosticCode code, string path)
    {
        var exception = Assert.Throws<FlowCompilationException>(action);
        Assert.Multiple(() =>
        {
            Assert.That(exception!.Diagnostics[0].Code, Is.EqualTo(code));
            Assert.That(exception.Diagnostics[0].Path, Is.EqualTo(path));
        });
    }

    /// <summary>
    /// Compiles the specified flow source for a fixture and updates its generated
    /// artifacts when fixture regeneration is enabled.
    /// </summary>
    /// <param name="fixture">The name of the fixture.</param>
    /// <param name="source">The executable flow source to compile.</param>
    /// <returns>The compiled flow result.</returns>
    private FlowCompilationResult CompileFixture(
        string fixture,
        ExecutableFlowSource source)
    {
        var request = BuildCompilationRequest(source);
        var result = _compiler.Compile(request);

        FixtureUpdater.UpdateFlowCompilation(
            fixture,
            result,
            FixtureUpdateRoot,
            _compiler);

        return result;
    }

    /// <summary>
    /// Loads and compiles the specified flow fixture and updates its generated
    /// artifacts when fixture regeneration is enabled.
    /// </summary>
    /// <param name="fixture">The name of the fixture to load and compile.</param>
    /// <returns>The compiled flow result.</returns>
    private FlowCompilationResult CompileFixture(string fixture)
    {
        var source = ReadSource(fixture);
        return CompileFixture(fixture, source);
    }
}