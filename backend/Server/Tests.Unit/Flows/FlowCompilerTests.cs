using Server.Services;
using Server.Services.Contracts;
using Server.Services.Implementation;
using System.Text;
using System.Text.Json;

namespace Tests.Unit.Flows;

public sealed class FlowCompilerTests
{
    private static readonly string[] TutorialKinds =
    [
        "add", "analogInput", "analogOutput", "and", "average", "calculator", "calendar",
        "clamp", "comparator", "delay", "digitalConstant", "digitalInput", "digitalOutput",
        "flowInput", "flowOutput", "if", "line", "levelShifter", "max", "memory", "min",
        "nand", "nor", "not", "numericConstant", "onDelay", "or", "override", "pulse",
        "qualityGood", "risingEdge", "schedule", "selector", "sequence", "split", "timer",
        "xnor", "xor"
    ];
    private static readonly string SourceFixtureRoot = Path.Combine(
        AppContext.BaseDirectory,
        "ContractFixtures",
        "flow-il-v1");
    private static readonly string ExpectedFixtureRoot = Path.Combine(
        AppContext.BaseDirectory,
        "ContractFixtures",
        "flow-il-v1");

    /// <summary>
    /// Purpose: Ensures every executable tutorial function has a compiler-valid ordinary flow fixture.
    /// Description: Builds a minimal fully-driven graph for each canonical kind and compiles it through Flow IL.
    /// </summary>
    [TestCaseSource(nameof(TutorialKinds))]
    public void EveryExecutableTutorialKindCompilesThroughTheNormalCompiler(string kind)
    {
        // Arrange: Create a current-schema fixture with typed constant drivers and canonical configuration.
        var source = TutorialSource(kind);

        // Act: Compile through the production compiler rather than tutorial-specific semantics.
        var compilation = new FlowCompiler().Compile(Request(source));

        // Assert: A bounded current artifact and stable source identity are produced.
        Assert.Multiple(() =>
        {
            Assert.That(compilation.Artifact.Length, Is.GreaterThan(0));
            Assert.That(compilation.Artifact.Length, Is.LessThanOrEqualTo(16_384));
            Assert.That(compilation.NodeIndices, Does.ContainKey("tutorial-node"));
        });
    }

    [TestCase("valid-two-button-and")]
    [TestCase("valid-source-order-permutation")]
    [TestCase("valid-memory-feedback")]
    public void CompilesGoldenSourceToTheExactCanonicalArtifact(string fixture)
    {
        var source = ReadSource(fixture);

        var result = new FlowCompiler().Compile(Request(source));
        var expected = File.ReadAllBytes(Path.Combine(ExpectedFixtureRoot, fixture, "artifact.bin"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Artifact.ToArray(), Is.EqualTo(expected));
            Assert.That(result.ArtifactSha256,
                Is.EqualTo(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(expected))));
            Assert.That(result.ArtifactVersion, Is.EqualTo(1));
            Assert.That(result.NodeIndices.Keys, Is.EqualTo(result.Schedule));
        });
    }

    [TestCase("nand", 9)]
    [TestCase("nor", 10)]
    [TestCase("xor", 11)]
    [TestCase("xnor", 12)]
    public void LowersExpandedBooleanNodesToTheirNormativeOpcode(string kind, byte opcode)
    {
        var source = ReadSource("valid-two-button-and");
        source = source with
        {
            Nodes = [.. source.Nodes.Select(node => node.Kind == "and" ? node with { Kind = kind } : node)]
        };

        var artifact = new FlowCompiler().Compile(Request(source)).Artifact.ToArray();
        var instructionSection = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(
            artifact.AsSpan(128 + (3 * 48) + 4, 4));

        var instructionCount = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(artifact.AsSpan(32, 4));
        Assert.That(
            Enumerable.Range(0, checked((int)instructionCount))
                .Select(index => artifact[checked((int)instructionSection) + (index * 12)]),
            Does.Contain(opcode));
    }

    [Test]
    public void RejectsUnsupportedNodesWithAStableGraphPath()
    {
        var source = ReadSource("valid-two-button-and") with
        {
            Nodes =
            [
                ReadSource("valid-two-button-and").Nodes[0] with { Kind = "unknownNode" }
            ],
            Connections = []
        };

        AssertDiagnostic(
            () => new FlowCompiler().Compile(Request(source)),
            "unsupported_node",
            "/nodes/0/kind");
    }

    [Test]
    public void RejectsCombinationalCyclesWithTheLexicallyFirstNodePath()
    {
        var source = ReadSource("valid-two-button-and") with
        {
            Nodes =
            [
                new ExecutableFlowNode { Id = "not-a", Kind = "not" },
                new ExecutableFlowNode { Id = "not-b", Kind = "not" }
            ],
            Connections =
            [
                new ExecutableFlowConnection(new ExecutableFlowEndpoint("not-a", "value"), new ExecutableFlowEndpoint("not-b", "in")),
                new ExecutableFlowConnection(new ExecutableFlowEndpoint("not-b", "value"), new ExecutableFlowEndpoint("not-a", "in"))
            ]
        };

        AssertDiagnostic(
            () => new FlowCompiler().Compile(Request(source)),
            "combinational_cycle",
            "/nodes/not-a");
    }

    [Test]
    public void RejectsMissingInputDriversBeforeEncoding()
    {
        var source = ReadSource("valid-two-button-and") with { Connections = [] };

        AssertDiagnostic(
            () => new FlowCompiler().Compile(Request(source)),
            "missing_connection",
            "/nodes/and-main/ports/a");
    }

    [Test]
    public void ReportsScheduledResourceRequirementsForThePlcScan()
    {
        var result = new FlowCompiler().Compile(Request(ReadSource("valid-memory-feedback")));

        Assert.Multiple(() =>
        {
            Assert.That(result.Schedule, Is.EqualTo(new[]
            {
                "constant-true",
                "memory-1",
                "or-1",
                "output-01-node"
            }));
            Assert.That(result.MaximumWorkPerScan, Is.EqualTo(6));
            Assert.That(result.WorkingBytes, Is.EqualTo(160));
            Assert.That(result.MaximumSnapshotBytes, Is.EqualTo(16384));
        });
    }

    [TestCase(0)]
    [TestCase(99)]
    public void RejectsEveryNonCurrentArtifactVersionWithAStablePath(int artifactVersion)
    {
        var request = Request(ReadSource("valid-two-button-and")) with { ArtifactVersion = artifactVersion };

        AssertDiagnostic(
            () => new FlowCompiler().Compile(request),
            "unsupported_artifact_version",
            "/artifactVersion");
    }

    [Test]
    public void CapturesResolvedPointRevisionsInTheCanonicalArtifact()
    {
        var source = ReadSource("valid-two-button-and");
        var firstRequest = Request(source);
        var first = new FlowCompiler().Compile(firstRequest);
        var changedPoint = firstRequest.Target.Points[0] with { Revision = 2 };
        var second = new FlowCompiler().Compile(firstRequest with
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
                Inputs = [new FlowInterfaceInput { Id = "temperature", Name = "Temperature", DataType = "number", Units = "°C", Required = true }],
                Outputs = [new FlowInterfaceOutput { Id = "result", Name = "Result", DataType = "number", Units = "°C" }]
            },
            Nodes =
            [
                new ExecutableFlowNode { Id = "input", Kind = "flowInput", Configuration = new Dictionary<string, JsonElement> { ["interfaceId"] = JsonSerializer.SerializeToElement("temperature") } },
                new ExecutableFlowNode { Id = "output", Kind = "flowOutput", Configuration = new Dictionary<string, JsonElement> { ["interfaceId"] = JsonSerializer.SerializeToElement("result") } }
            ],
            Connections = [new ExecutableFlowConnection(new ExecutableFlowEndpoint("input", "value"), new ExecutableFlowEndpoint("output", "value"))]
        };
        var request = Request(source);

        var first = new FlowCompiler().Compile(request);
        var second = new FlowCompiler().Compile(request);

        Assert.Multiple(() =>
        {
            Assert.That(first.Artifact.ToArray(), Is.EqualTo(second.Artifact.ToArray()));
            Assert.That(first.Schedule, Is.EqualTo(new[] { "input", "output" }));
            Assert.That(first.NodeIndices.Keys, Is.EquivalentTo(new[] { "input", "output" }));
            Assert.That(Encoding.UTF8.GetString(first.Artifact.Span), Does.Contain("temperature"));
            Assert.That(Encoding.UTF8.GetString(first.Artifact.Span), Does.Contain("result"));
        });
    }

    [Test]
    public void RejectsAnUnresolvedPointDependencyBeforeEmission()
    {
        var source = ReadSource("valid-two-button-and");
        var request = Request(source);
        request = request with { Target = request.Target with { Points = [.. request.Target.Points.Skip(1)] } };

        AssertDiagnostic(
            () => new FlowCompiler().Compile(request),
            "missing_point",
            $"/points/{source.Nodes[0].Configuration["pointId"].GetString()}");
    }

    private static ExecutableFlowSource TutorialSource(string kind)
    {
        var numericInputs = kind switch
        {
            "add" or "comparator" or "min" or "max" => new[] { "a", "b" },
            "selector" => new[] { "a", "b" },
            "average" or "calculator" or "clamp" or "line" => new[] { "input" },
            "levelShifter" => new[] { "in" },
            "analogOutput" => new[] { "in" },
            _ => []
        };
        var booleanInputs = kind switch
        {
            "not" or "qualityGood" or "onDelay" or "risingEdge" or "memory" => new[] { "in" },
            "and" or "or" or "nand" or "nor" or "xnor" or "xor" or "sequence" => new[] { "a", "b" },
            "if" => new[] { "condition", "whenTrue", "whenFalse" },
            "selector" => new[] { "condition" },
            "split" or "override" or "delay" or "timer" or "pulse" => new[] { "input" },
            "digitalOutput" => new[] { "in" },
            "flowOutput" => new[] { "value" },
            _ => []
        };
        var configuration = kind switch
        {
            "digitalInput" or "digitalOutput" or "analogInput" or "analogOutput" => Config("pointId", "tutorial-point"),
            "digitalConstant" or "memory" => Config("value", true),
            "numericConstant" => Config("value", 1D),
            "comparator" => Config("operator", "gt"),
            "levelShifter" or "line" => Config(("gain", 1D), ("offset", 0D)),
            "onDelay" or "delay" or "timer" => Config("durationMs", 100D),
            "clamp" => Config(("minimum", 0D), ("maximum", 100D)),
            "schedule" or "calendar" => Config("enabled", true),
            "flowInput" or "flowOutput" => Config("interfaceId", kind == "flowInput" ? "tutorial-input" : "tutorial-output"),
            _ => new Dictionary<string, JsonElement>()
        };
        var nodes = new List<ExecutableFlowNode>();
        var connections = new List<ExecutableFlowConnection>();
        foreach (var port in numericInputs)
        {
            var id = $"number-{port}";
            nodes.Add(new ExecutableFlowNode { Id = id, Kind = "numericConstant", Configuration = Config("value", 1D) });
            connections.Add(new ExecutableFlowConnection(new ExecutableFlowEndpoint(id, "value"), new ExecutableFlowEndpoint("tutorial-node", port)));
        }
        foreach (var port in booleanInputs)
        {
            var id = $"boolean-{port}";
            nodes.Add(new ExecutableFlowNode { Id = id, Kind = "digitalConstant", Configuration = Config("value", true) });
            connections.Add(new ExecutableFlowConnection(new ExecutableFlowEndpoint(id, "value"), new ExecutableFlowEndpoint("tutorial-node", port)));
        }
        nodes.Add(new ExecutableFlowNode { Id = "tutorial-node", Kind = kind, Configuration = configuration });
        return new ExecutableFlowSource
        {
            Id = "tutorial",
            Revision = 1,
            ControllerTemplateId = "fixture",
            ControllerTemplateRevision = 1,
            Nodes = nodes,
            Connections = connections,
            Interface = new FlowInterface
            {
                Inputs = [new FlowInterfaceInput { Id = "tutorial-input", Name = "Tutorial input", DataType = "boolean", Required = false }],
                Outputs = [new FlowInterfaceOutput { Id = "tutorial-output", Name = "Tutorial output", DataType = "boolean" }]
            }
        };
    }

    private static Dictionary<string, JsonElement> Config(string key, object value) =>
        new() { [key] = JsonSerializer.SerializeToElement(value) };

    private static Dictionary<string, JsonElement> Config(params (string Key, object Value)[] values) =>
        values.ToDictionary(value => value.Key, value => JsonSerializer.SerializeToElement(value.Value), StringComparer.Ordinal);

    private static ExecutableFlowSource ReadSource(string fixture)
    {
        var json = File.ReadAllText(Path.Combine(SourceFixtureRoot, fixture, "source-flow.json"));
        return JsonSerializer.Deserialize<ExecutableFlowSource>(json, FlowControlJson.Options)!;
    }

    private static FlowCompilationRequest Request(ExecutableFlowSource source) => new()
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
                new HashSet<PointValueType> { PointValueType.Digital, PointValueType.Analog },
                new HashSet<PointDirection> { PointDirection.Input, PointDirection.Output },
                new HashSet<ControllerPointFeature>(),
                new HashSet<ConnectorDataType> { ConnectorDataType.Boolean, ConnectorDataType.Number },
                new HashSet<string>(StringComparer.Ordinal),
                new HashSet<ExecutionMode>(),
                new HashSet<ControllerRuntimeFeature>()),
            Points = [.. source.Nodes
                .Where(node => node.Kind is "digitalInput" or "digitalOutput" or "analogInput" or "analogOutput")
                .Select(node => new Point
                {
                    Id = node.Configuration["pointId"].GetString()!,
                    Name = node.Configuration["pointId"].GetString()!,
                    Enabled = true,
                    Implementation = "virtual",
                    Direction = node.Kind is "digitalInput" or "analogInput" ? "input" : "output",
                    ValueType = node.Kind is "analogInput" or "analogOutput" ? "analog" : "digital",
                    Readable = node.Kind is "digitalInput" or "analogInput",
                    Commandable = node.Kind is "digitalOutput" or "analogOutput",
                    Persistence = "volatile",
                    Revision = 1
                })
                .DistinctBy(point => point.Id, StringComparer.Ordinal)]
        }
    };

    private static void AssertDiagnostic(TestDelegate action, string code, string path)
    {
        var exception = Assert.Throws<FlowCompilationException>(action);
        Assert.Multiple(() =>
        {
            Assert.That(exception!.Diagnostics[0].Code, Is.EqualTo(code));
            Assert.That(exception.Diagnostics[0].Path, Is.EqualTo(path));
        });
    }
}