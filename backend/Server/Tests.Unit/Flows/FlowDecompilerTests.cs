using Server.Services;
using Server.Services.Contracts;
using Server.Services.Implementation;
using Server.Services.Implementation.Compiler;
using System.Text.Json;
using Tests.Unit.Helpers;

namespace Tests.Unit.Flows;

public sealed class FlowDecompilerTests
{
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
    public void UpdateEnabledDecompilerFixtures()
    {
        foreach (var (fixture, analogUnits) in DecompilerFixtures)
        {
            if (!FixtureUpdater.IsEnabled(fixture))
            {
                continue;
            }

            var source = ReadSourceFixture(fixture);

            var compilation = new FlowCompiler().Compile(
                CompilationRequest(source, analogUnits));

            // Write source-controlled artifacts.
            FixtureUpdater.UpdateFlowCompilation(
                fixture,
                compilation,
                FixtureUpdateRoot);

            // Write the runtime copy used by this test process.
            var runtimeFixtureDirectory =
                Path.Combine(FixtureRoot, fixture);

            Directory.CreateDirectory(runtimeFixtureDirectory);

            FlowCompiler.WriteBinary(
                compilation,
                Path.Combine(runtimeFixtureDirectory, "artifact.bin"));

            FlowCompiler.WriteIntelHex(
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
        var recovered = new FlowDecompiler().Decompile(artifact);
        var source = RecoveredSource(recovered);

        var recompiled = new FlowCompiler().Compile(
            CompilationRequest(source, analogUnits));

        AssertArtifactsEqual(
            artifact,
            recompiled.Artifact.ToArray());
    }

    [TestCase("valid-two-button-and", 4, 3)]
    [TestCase("valid-memory-feedback", 3, 2)]
    public void RecoversAValidDeterministicDesignerFlow(string fixture, int nodeCount, int connectionCount)
    {
        var artifact = GetArtifact(fixture);
        var decompiler = new FlowDecompiler();

        var first = decompiler.Decompile(artifact);
        var second = decompiler.Decompile(artifact);

        Assert.Multiple(() =>
        {
            Assert.That(
                System.Text.Json.JsonSerializer.Serialize(first, FlowControlJson.Options),
                Is.EqualTo(System.Text.Json.JsonSerializer.Serialize(second, FlowControlJson.Options)));
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
        var result = new FlowDecompiler().Decompile(artifact);
        var memory = result.Flow.Nodes.Single(node => node.Id == "memory-1");

        Assert.Multiple(() =>
        {
            Assert.That(memory.Kind, Is.EqualTo(FlowNodeKind.Memory));
            Assert.That(memory.Configuration["value"].GetDouble(), Is.EqualTo(2));
            Assert.That(result.Flow.Nodes.Single(node => node.Id == "output-01-node").Configuration["pointId"].GetString(), Is.EqualTo("output-01"));
            Assert.That(result.Flow.Connections.Any(connection =>
                connection.Start.NodeId == "constant-2"
                && connection.End == new FlowEndpoint("memory-1", "in")), Is.True);
        });
    }

    [Test]
    public void PreservesLosslessGroupAndCanvasMetadata()
    {
        var result = new FlowDecompiler().Decompile(GetArtifact("valid-analog-points"));

        Assert.Multiple(() =>
        {
            Assert.That(result.RecoveryLevel, Is.EqualTo("lossless"));
            Assert.That(result.Flow.Nodes.Single(node => node.Id == "shift").GroupId, Is.EqualTo("conditioning"));
            Assert.That(result.Warnings, Is.Empty);
        });
    }

    [Test]
    public void RejectsCorruptArtifactsBeforeReadingInstructions()
    {
        var artifact = GetArtifact("valid-two-button-and");
        artifact[^1] ^= 1;

        var exception = Assert.Throws<FlowDecompilationException>(
            () => new FlowDecompiler().Decompile(artifact));

        Assert.That(exception!.Diagnostic.Code, Is.EqualTo(FlowCompilationDiagnosticCode.InvalidDigest));
    }

    [Test]
    public void RejectsUnsupportedArtifactVersionsWithAStablePath()
    {
        var artifact = GetArtifact("valid-two-button-and");
        artifact[4] = 3;

        var exception = Assert.Throws<FlowDecompilationException>(
            () => new FlowDecompiler().Decompile(artifact));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Diagnostic.Code, Is.EqualTo(FlowCompilationDiagnosticCode.UnsupportedVersion));
            Assert.That(exception.Diagnostic.Path, Is.EqualTo("/version"));
        });
    }

    private static byte[] GetArtifact(string fixture) =>
        File.ReadAllBytes(Path.Combine(FixtureRoot, fixture, "artifact.bin"));

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
                    Revision = checked((int)source.ControllerTemplateRevision)
                },
                new HashSet<PointValueType>
                {
                    PointValueType.Digital,
                    PointValueType.Analog
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
                    .Where(node => node.Kind is
                        FlowNodeKind.DigitalInput or
                        FlowNodeKind.DigitalOutput or
                        FlowNodeKind.AnalogInput or
                        FlowNodeKind.AnalogOutput)
                    .Select(node => new Point
                    {
                        Id = node.Configuration["pointId"].GetString()!,
                        Name = node.Configuration["pointId"].GetString()!,
                        Enabled = true,
                        Implementation = "virtual",
                        Direction = node.Kind.ToString().EndsWith("Input", StringComparison.Ordinal)
                            ? DataDirection.Input
                            : DataDirection.Output,
                        ValueType = node.Kind.ToString().StartsWith("analog", StringComparison.Ordinal)
                            ? PointValueType.Analog
                            : PointValueType.Digital,
                        Units = node.Kind.ToString().StartsWith("analog", StringComparison.Ordinal)
                            ? analogUnits
                            : null,
                        Readable = node.Kind.ToString().EndsWith("Input", StringComparison.Ordinal),
                        Commandable = node.Kind.ToString().EndsWith("Output", StringComparison.Ordinal),
                        Persistence = "volatile",
                        Revision = 1
                    })
                    .DistinctBy(point => point.Id, StringComparer.Ordinal)
            ]
            }
        };

    private static void AssertArtifactsEqual(
        byte[] expected,
        byte[] actual)
    {
        var commonLength = Math.Min(expected.Length, actual.Length);

        var firstDifference = Enumerable.Range(0, commonLength)
            .FirstOrDefault(index => expected[index] != actual[index], -1);

        if (firstDifference < 0 && expected.Length == actual.Length)
        {
            return;
        }

        if (firstDifference < 0)
        {
            firstDifference = commonLength;
        }

        const int context = 16;

        var start = Math.Max(0, firstDifference - context);
        var expectedCount = Math.Min(expected.Length - start, context * 2);
        var actualCount = Math.Min(actual.Length - start, context * 2);

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