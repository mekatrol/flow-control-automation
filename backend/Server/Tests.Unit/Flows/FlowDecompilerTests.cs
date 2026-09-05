using Server.Common.Contracts;
using Server.Common.Models;
using Server.Compiler;
using Server.Compiler.Contracts;
using Server.Compiler.Extensions;
using Server.Compiler.Services;
using Server.Services.Contracts;
using System.Text.Json;
using Tests.Unit.Helpers;

namespace Tests.Unit.Flows;

public sealed class FlowDecompilerTests
{
    private ServiceProvider _serviceProvider = null!;
    private IFlowCompiler _compiler = null!;
    private IFlowDecompiler _decompiler = null!;

    private static readonly string FixtureRoot = Path.Combine(
        AppContext.BaseDirectory,
        "ContractFixtures",
        "flow-il-v1");

    private static readonly string FixtureUpdateRoot = Path.Combine(
        FixtureUpdater.WorkspaceDirectory(),
        "testdata",
        "contracts",
        "flow-il-v1");

    private static readonly (string Fixture, string? AnalogUnits)[] DecompilerFixtures =
    [
        ("valid-two-button-and", null),
        ("valid-memory-feedback", null),
        ("valid-expanded-boolean", null),
        ("valid-numeric-language", null),
        ("valid-analog-points", "degC")
    ];

    private static ExecutableFlowSource RecoveredSource(
        FlowDecompilationResult recovered)
    {
        return new ExecutableFlowSource
        {
            Id = recovered.Flow.Id,
            Revision = recovered.Provenance.FlowRevision,
            ControllerTemplateId = recovered.Provenance.ControllerTemplateId,
            ControllerTemplateRevision = recovered.Provenance.ControllerTemplateRevision,

            Nodes =
            [
                .. recovered.Flow.Nodes.Select(node => new ExecutableFlowNode
                {
                    Id = node.Id,
                    Kind = node.Kind,
                    Configuration = node.Configuration,
                    Label = node.Label,
                    X = node.X,
                    Y = node.Y,
                    ZOrder = node.ZOrder,
                    GroupId = node.GroupId
                })
            ],

            Connections =
            [
                .. recovered.Flow.Connections.Select(connection =>
                    new ExecutableFlowConnection(
                        new ExecutableFlowEndpoint(
                            connection.Start.NodeId,
                            connection.Start.ConnectorId),
                        new ExecutableFlowEndpoint(
                            connection.End.NodeId,
                            connection.End.ConnectorId)))
            ]
        };
    }

    private static ExecutableFlowSource ReadSourceFixture(string fixture)
    {
        var path = Path.Combine(
            FixtureUpdateRoot,
            fixture,
            "source-flow.json");

        var json = File.ReadAllText(path);

        return JsonSerializer.Deserialize<ExecutableFlowSource>(
            json,
            FlowControlJson.Options)!;
    }

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var services = new ServiceCollection();
        services.AddFlowCompilerServices();

        _serviceProvider = services.BuildServiceProvider();

        _compiler = _serviceProvider.GetRequiredService<IFlowCompiler>();
        _decompiler = _serviceProvider.GetRequiredService<IFlowDecompiler>();

        UpdateEnabledDecompilerFixtures();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _serviceProvider.Dispose();
    }

    private void UpdateEnabledDecompilerFixtures()
    {
        foreach (var (fixture, analogUnits) in DecompilerFixtures)
        {
            if (!FixtureUpdater.IsEnabled(fixture))
            {
                continue;
            }

            var source = ReadSourceFixture(fixture);

            var compilation = _compiler.Compile(
                CompilationRequest(source, analogUnits));

            // Write source-controlled artifacts.
            FixtureUpdater.UpdateFlowCompilation(
                fixture,
                compilation,
                FixtureUpdateRoot,
                _compiler);

            // Write the runtime copy used by this test process.
            var runtimeFixtureDirectory =
                Path.Combine(FixtureRoot, fixture);

            Directory.CreateDirectory(runtimeFixtureDirectory);

            _compiler.WriteBinary(
                compilation,
                Path.Combine(runtimeFixtureDirectory, "artifact.bin"));

            _compiler.WriteIntelHex(
                compilation,
                Path.Combine(runtimeFixtureDirectory, "artifact.hex"));
        }
    }

    [TestCase("valid-two-button-and", null)]
    [TestCase("valid-memory-feedback", null)]
    [TestCase("valid-expanded-boolean", null)]
    [TestCase("valid-numeric-language", null)]
    [TestCase("valid-analog-points", "degC")]
    public void RecompilesRecoveredDesignerSemanticsToTheIdenticalArtifact(
        string fixture,
        string? analogUnits)
    {
        var artifact = GetArtifact(fixture);
        var recovered = _decompiler.Decompile(artifact);
        var source = RecoveredSource(recovered);

        var recompiled = _compiler.Compile(
            CompilationRequest(source, analogUnits));

        AssertArtifactsEqual(
            artifact,
            recompiled.Artifact.ToArray());
    }

    [TestCase("valid-two-button-and", 4, 3)]
    [TestCase("valid-memory-feedback", 3, 2)]
    public void RecoversAValidDeterministicDesignerFlow(
        string fixture,
        int nodeCount,
        int connectionCount)
    {
        var artifact = GetArtifact(fixture);

        var first = _decompiler.Decompile(artifact);
        var second = _decompiler.Decompile(artifact);

        Assert.Multiple(() =>
        {
            Assert.That(
                JsonSerializer.Serialize(first, FlowControlJson.Options),
                Is.EqualTo(
                    JsonSerializer.Serialize(second, FlowControlJson.Options)));

            Assert.That(first.RecoveryLevel, Is.EqualTo("lossless"));
            Assert.That(first.Flow.Nodes, Has.Count.EqualTo(nodeCount));
            Assert.That(first.Flow.Connections, Has.Count.EqualTo(connectionCount));
            Assert.That(first.Warnings, Is.Empty);
            Assert.That(first.Provenance.ArtifactVersion, Is.EqualTo(1));
        });
    }

    [Test]
    public void PreservesExecutableNodeIdentityConfigurationAndFeedback()
    {
        var artifact = GetArtifact("valid-memory-feedback");
        var result = _decompiler.Decompile(artifact);
        var memory = result.Flow.Nodes.Single(node => node.Id == "memory-1");

        Assert.Multiple(() =>
        {
            Assert.That(
                memory.Kind,
                Is.EqualTo(FlowNodeKind.Memory));

            Assert.That(
                memory.Configuration["value"].GetDouble(),
                Is.EqualTo(2));

            Assert.That(
                result.Flow.Nodes
                    .Single(node => node.Id == "output-01-node")
                    .Configuration["pointId"]
                    .GetString(),
                Is.EqualTo("output-01"));

            Assert.That(
                result.Flow.Connections.Any(connection =>
                    connection.Start.NodeId == "constant-2"
                    && connection.End ==
                    new FlowEndpoint("memory-1", "in")),
                Is.True);
        });
    }

    [Test]
    public void PreservesLosslessGroupAndCanvasMetadata()
    {
        var result =
            _decompiler.Decompile(
                GetArtifact("valid-analog-points"));

        Assert.Multiple(() =>
        {
            Assert.That(
                result.RecoveryLevel,
                Is.EqualTo("lossless"));

            Assert.That(
                result.Flow.Nodes
                    .Single(node => node.Id == "shift")
                    .GroupId,
                Is.EqualTo("conditioning"));

            Assert.That(result.Warnings, Is.Empty);
        });
    }

    [Test]
    public void RecoversCalculatorFormulaInputsAndIdenticalArithmeticIl()
    {
        var source = new ExecutableFlowSource
        {
            Id = "calculator-round-trip",
            Revision = 7,
            ControllerTemplateId = "calculator-target",
            ControllerTemplateRevision = 1,
            Nodes =
            [
                Constant("constant-a", 2),
                Constant("constant-b", 3),
                Constant("constant-c", 4),
                new ExecutableFlowNode
                {
                    Id = "calculator",
                    Kind = FlowNodeKind.Calculator,
                    Label = "BODMAS calculator",
                    X = 120,
                    Y = 80,
                    ZOrder = 3,
                    Configuration = Configuration("formula", "a + b * (c - a) ^ b")
                }
            ],
            Connections =
            [
                Connection("constant-a", "a"),
                Connection("constant-b", "b"),
                Connection("constant-c", "c")
            ]
        };
        var original = _compiler.Compile(CompilationRequest(source, null));

        var recovered = _decompiler.Decompile(original.Artifact);
        var calculator = recovered.Flow.Nodes.Single(node => node.Id == "calculator");
        var recompiled = _compiler.Compile(CompilationRequest(RecoveredSource(recovered), null));

        Assert.Multiple(() =>
        {
            Assert.That(calculator.Kind, Is.EqualTo(FlowNodeKind.Calculator));
            Assert.That(calculator.Configuration["formula"].GetString(),
                Is.EqualTo("(a + (b * ((c - a) ^ b)))"));
            Assert.That(calculator.Connectors.Select(connector => connector.Id),
                Is.EqualTo(new[] { "a", "b", "c", "output" }));
            Assert.That(recovered.Flow.Connections
                .Where(connection => connection.End.NodeId == "calculator")
                .Select(connection => connection.End.ConnectorId),
                Is.EquivalentTo(["a", "b", "c"]));
        });
        AssertArtifactsEqual(original.Artifact.ToArray(), recompiled.Artifact.ToArray());

        static ExecutableFlowNode Constant(string id, double value) => new()
        {
            Id = id,
            Kind = FlowNodeKind.AnalogConstant,
            Label = id,
            Configuration = Configuration("value", value)
        };

        static ExecutableFlowConnection Connection(string sourceId, string port) => new(
            new ExecutableFlowEndpoint(sourceId, "value"),
            new ExecutableFlowEndpoint("calculator", port));
    }

    [TestCase(FlowNodeKind.Subtract)]
    [TestCase(FlowNodeKind.Multiply)]
    [TestCase(FlowNodeKind.Divide)]
    [TestCase(FlowNodeKind.Power)]
    [TestCase(FlowNodeKind.Negate)]
    public void RoundTripsDiscreteArithmeticNodesAsPrimaryInstructions(FlowNodeKind kind)
    {
        var unary = kind == FlowNodeKind.Negate;
        var source = new ExecutableFlowSource
        {
            Id = $"round-trip-{kind}",
            Revision = 1,
            ControllerTemplateId = "arithmetic-target",
            ControllerTemplateRevision = 1,
            Nodes = unary
                ? [ArithmeticConstant("a", 8), ArithmeticNode(kind)]
                : [ArithmeticConstant("a", 8), ArithmeticConstant("b", 3), ArithmeticNode(kind)],
            Connections = unary
                ? [ArithmeticConnection("a", "in")]
                : [ArithmeticConnection("a", "a"), ArithmeticConnection("b", "b")]
        };
        var original = _compiler.Compile(CompilationRequest(source, null));

        var recovered = _decompiler.Decompile(original.Artifact);
        var recompiled = _compiler.Compile(CompilationRequest(RecoveredSource(recovered), null));

        Assert.That(recovered.Flow.Nodes.Single(node => node.Id == "operation").Kind, Is.EqualTo(kind));
        AssertArtifactsEqual(original.Artifact.ToArray(), recompiled.Artifact.ToArray());

        static ExecutableFlowNode ArithmeticConstant(string id, double value) => new()
        {
            Id = id,
            Kind = FlowNodeKind.AnalogConstant,
            Label = id,
            Configuration = Configuration("value", value)
        };
        static ExecutableFlowNode ArithmeticNode(FlowNodeKind kind) => new()
        {
            Id = "operation",
            Kind = kind,
            Label = kind.ToString()
        };
        static ExecutableFlowConnection ArithmeticConnection(string source, string port) => new(
            new ExecutableFlowEndpoint(source, "value"),
            new ExecutableFlowEndpoint("operation", port));
    }

    [Test]
    public void RoundTripsClockFrequencyDutyCycleAndEnableConnection()
    {
        var source = new ExecutableFlowSource
        {
            Id = "round-trip-clock",
            Revision = 1,
            ControllerTemplateId = "clock-target",
            ControllerTemplateRevision = 1,
            Nodes =
            [
                new ExecutableFlowNode { Id = "enable", Kind = FlowNodeKind.DigitalConstant, Configuration = Configuration("value", true) },
                new ExecutableFlowNode
                {
                    Id = "clock",
                    Kind = FlowNodeKind.Clock,
                    Configuration = new Dictionary<string, JsonElement>
                    {
                        ["frequencyHz"] = JsonSerializer.SerializeToElement(2D),
                        ["dutyCycle"] = JsonSerializer.SerializeToElement(25D)
                    }
                }
            ],
            Connections = [new(new("enable", "value"), new("clock", "enable"))]
        };
        var original = _compiler.Compile(CompilationRequest(source, null));

        var recovered = _decompiler.Decompile(original.Artifact);
        var clock = recovered.Flow.Nodes.Single(node => node.Id == "clock");
        var recompiled = _compiler.Compile(CompilationRequest(RecoveredSource(recovered), null));

        Assert.Multiple(() =>
        {
            Assert.That(clock.Kind, Is.EqualTo(FlowNodeKind.Clock));
            Assert.That(clock.Configuration["frequencyHz"].GetDouble(), Is.EqualTo(2D));
            Assert.That(clock.Configuration["dutyCycle"].GetDouble(), Is.EqualTo(25D));
            Assert.That(recovered.Flow.Connections.Single().End.ConnectorId, Is.EqualTo("enable"));
        });
        AssertArtifactsEqual(original.Artifact.ToArray(), recompiled.Artifact.ToArray());
    }

    [Test]
    public void RejectsCorruptArtifactsBeforeReadingInstructions()
    {
        var artifact = GetArtifact("valid-two-button-and");
        artifact[^1] ^= 1;

        var exception =
            Assert.Throws<FlowDecompilationException>(
                () => _decompiler.Decompile(artifact));

        Assert.That(
            exception!.Diagnostic.Code,
            Is.EqualTo(
                FlowCompilationDiagnosticCode.InvalidSectionDigest));
    }

    [Test]
    public void RejectsUnsupportedArtifactVersionsWithAStablePath()
    {
        var artifact = GetArtifact("valid-two-button-and");
        artifact[4] = 3;

        var exception =
            Assert.Throws<FlowDecompilationException>(
                () => _decompiler.Decompile(artifact));

        Assert.Multiple(() =>
        {
            Assert.That(
                exception!.Diagnostic.Code,
                Is.EqualTo(
                    FlowCompilationDiagnosticCode.UnsupportedFlowIlVersion));

            Assert.That(
                exception.Diagnostic.Path,
                Is.EqualTo("/version"));
        });
    }

    private static byte[] GetArtifact(string fixture) =>
        File.ReadAllBytes(
            Path.Combine(
                FixtureRoot,
                fixture,
                "artifact.bin"));

    private static Dictionary<string, JsonElement> Configuration(string key, object value) =>
        new() { [key] = JsonSerializer.SerializeToElement(value) };

    private static FlowCompilationRequest CompilationRequest(
        ExecutableFlowSource source,
        string? analogUnits) => new()
        {
            Source = source,
            Target = new FlowCompilationTarget
            {
                ControllerTemplate = new ValidatedControllerTemplate(
                    new ControllerTemplate
                    {
                        Id = source.ControllerTemplateId,
                        Name = "Recovered target",
                        Revision =
                            checked(
                                (int)source.ControllerTemplateRevision)
                    },
                    new HashSet<FlowPointValueType>
                    {
                        FlowPointValueType.Digital,
                        FlowPointValueType.Analog
                    },
                    new HashSet<DataDirection>
                    {
                        DataDirection.Input,
                        DataDirection.Output
                    },
                    new HashSet<ControllerPointFeature>(),
                    new HashSet<ConnectorDataType>
                    {
                        ConnectorDataType.Boolean,
                        ConnectorDataType.Number
                    },
                    new HashSet<FlowFunctionKind>(),
                    new HashSet<ExecutionMode>(),
                    new HashSet<ControllerRuntimeFeature>()),

                Points =
                [
                    .. source.Nodes
                        .Where(node =>
                            node.Kind is
                                FlowNodeKind.DigitalInput or
                                FlowNodeKind.DigitalOutput or
                                FlowNodeKind.AnalogInput or
                                FlowNodeKind.AnalogOutput)
                        .Select(node => new FlowPoint
                        {
                            Id =
                                node.Configuration["pointId"]
                                    .GetString()!,

                            Name =
                                node.Configuration["pointId"]
                                    .GetString()!,

                            Enabled = true,
                            Implementation = "bound",

                            Direction =
                                node.Kind.ToString()
                                    .EndsWith(
                                        "Input",
                                        StringComparison.Ordinal)
                                    ? DataDirection.Input
                                    : DataDirection.Output,

                            ValueType =
                                node.Kind.ToString()
                                    .StartsWith(
                                        "analog",
                                        StringComparison.Ordinal)
                                    ? FlowPointValueType.Analog
                                    : FlowPointValueType.Digital,

                            PointSourceType = PointSourceType.Physical,

                            Units =
                                node.Kind.ToString()
                                    .StartsWith(
                                        "analog",
                                        StringComparison.Ordinal)
                                    ? analogUnits
                                    : null,

                            Readable =
                                node.Kind.ToString()
                                    .EndsWith(
                                        "Input",
                                        StringComparison.Ordinal),

                            Commandable =
                                node.Kind.ToString()
                                    .EndsWith(
                                        "Output",
                                        StringComparison.Ordinal),

                            Persistence = "volatile",
                            Revision = 1
                        })
                        .DistinctBy(
                            point => point.Id,
                            StringComparer.Ordinal)
                ]
            }
        };

    private static void AssertArtifactsEqual(
        byte[] expected,
        byte[] actual)
    {
        var commonLength =
            Math.Min(expected.Length, actual.Length);

        var firstDifference =
            Enumerable.Range(0, commonLength)
                .FirstOrDefault(
                    index => expected[index] != actual[index],
                    -1);

        if (firstDifference < 0
            && expected.Length == actual.Length)
        {
            return;
        }

        if (firstDifference < 0)
        {
            firstDifference = commonLength;
        }

        const int context = 16;

        var start =
            Math.Max(0, firstDifference - context);

        var expectedCount =
            Math.Min(
                expected.Length - start,
                context * 2);

        var actualCount =
            Math.Min(
                actual.Length - start,
                context * 2);

        Assert.Fail(
            $"""
                Artifacts differ.

                Expected length: {expected.Length} bytes
                Actual length:   {actual.Length} bytes
                Difference at:   {firstDifference} (0x{firstDifference:X})

                Expected:
                {Convert.ToHexString(expected.AsSpan(start, expectedCount))}

                Actual:
                {Convert.ToHexString(actual.AsSpan(start, actualCount))}
            """);
    }
}