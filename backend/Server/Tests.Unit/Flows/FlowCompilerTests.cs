using Server.Services;
using Server.Services.Contracts;
using Server.Services.Implementation;
using System.Text.Json;

namespace Tests.Unit.Flows;

public sealed class FlowCompilerTests
{
    private static readonly string SourceFixtureRoot = Path.Combine(
        AppContext.BaseDirectory,
        "ContractFixtures",
        "flow-il-v1");
    private static readonly string ExpectedFixtureRoot = Path.Combine(
        AppContext.BaseDirectory,
        "ContractFixtures",
        "flow-il-v1");

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
            Nodes = source.Nodes.Select(node => node.Kind == "and" ? node with { Kind = kind } : node).ToArray()
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
                ReadSource("valid-two-button-and").Nodes[0] with { Kind = "timer" }
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
    public void RejectsAnUnresolvedPointDependencyBeforeEmission()
    {
        var source = ReadSource("valid-two-button-and");
        var request = Request(source);
        request = request with { Target = request.Target with { Points = request.Target.Points.Skip(1).ToArray() } };

        AssertDiagnostic(
            () => new FlowCompiler().Compile(request),
            "missing_point",
            $"/points/{source.Nodes[0].Configuration["pointId"].GetString()}");
    }

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
                new HashSet<PointValueType> { PointValueType.Digital },
                new HashSet<PointDirection> { PointDirection.Input, PointDirection.Output },
                new HashSet<ControllerPointFeature>(),
                new HashSet<ConnectorDataType> { ConnectorDataType.Boolean },
                new HashSet<string>(StringComparer.Ordinal),
                new HashSet<ExecutionMode>(),
                new HashSet<ControllerRuntimeFeature>()),
            Points = source.Nodes
                .Where(node => node.Kind is "digitalInput" or "digitalOutput")
                .Select(node => new Point
                {
                    Id = node.Configuration["pointId"].GetString()!,
                    Name = node.Configuration["pointId"].GetString()!,
                    Enabled = true,
                    Implementation = "virtual",
                    Direction = node.Kind == "digitalInput" ? "input" : "output",
                    ValueType = "digital",
                    Readable = node.Kind == "digitalInput",
                    Commandable = node.Kind == "digitalOutput",
                    Persistence = "volatile",
                    Revision = 1
                })
                .DistinctBy(point => point.Id, StringComparer.Ordinal)
                .ToArray()
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
