using Server.Services;
using Server.Services.Contracts;
using Server.Services.Implementation;

namespace Tests.Unit.Flows;

public sealed class FlowDecompilerTests
{
    private static readonly string FixtureRoot = Path.Combine(
        AppContext.BaseDirectory,
        "ContractFixtures",
        "flow-il-v2");

    [TestCase("valid-two-button-and")]
    [TestCase("valid-memory-feedback")]
    public void RecompilesRecoveredDesignerSemanticsToTheIdenticalArtifact(string fixture)
    {
        var artifact = Artifact(fixture);
        var recovered = new FlowDecompiler().Decompile(artifact);
        var source = new ExecutableFlowSource
        {
            Id = recovered.Flow.Id,
            Revision = recovered.Provenance.FlowRevision,
            ControllerTemplateId = recovered.Provenance.ControllerTemplateId,
            ControllerTemplateRevision = recovered.Provenance.ControllerTemplateRevision,
            Nodes = recovered.Flow.Nodes.Select(node => new ExecutableFlowNode
            {
                Id = node.Id,
                Kind = node.Kind,
                Configuration = node.Configuration
            }).ToArray(),
            Connections = recovered.Flow.Connections.Select(connection => new ExecutableFlowConnection(
                new ExecutableFlowEndpoint(connection.Start.NodeId, connection.Start.ConnectorId),
                new ExecutableFlowEndpoint(connection.End.NodeId, connection.End.ConnectorId))).ToArray()
        };

        var recompiled = new FlowCompiler().Compile(CompilationRequest(source));

        Assert.That(recompiled.Artifact.ToArray(), Is.EqualTo(artifact));
    }

    [TestCase("valid-two-button-and", 4, 3)]
    [TestCase("valid-memory-feedback", 4, 4)]
    public void RecoversAValidDeterministicDesignerFlow(string fixture, int nodeCount, int connectionCount)
    {
        var artifact = Artifact(fixture);
        var decompiler = new FlowDecompiler();

        var first = decompiler.Decompile(artifact);
        var second = decompiler.Decompile(artifact);

        Assert.Multiple(() =>
        {
            Assert.That(
                System.Text.Json.JsonSerializer.Serialize(first, FlowControlJson.Options),
                Is.EqualTo(System.Text.Json.JsonSerializer.Serialize(second, FlowControlJson.Options)));
            Assert.That(first.RecoveryLevel, Is.EqualTo("normalized"));
            Assert.That(first.Flow.Nodes, Has.Count.EqualTo(nodeCount));
            Assert.That(first.Flow.Connections, Has.Count.EqualTo(connectionCount));
            Assert.That(first.Warnings, Has.Count.EqualTo(1));
            Assert.That(first.Provenance.ArtifactVersion, Is.EqualTo(2));
        });
    }

    [Test]
    public void PreservesExecutableNodeIdentityConfigurationAndFeedback()
    {
        var result = new FlowDecompiler().Decompile(Artifact("valid-memory-feedback"));
        var memory = result.Flow.Nodes.Single(node => node.Id == "memory-1");

        Assert.Multiple(() =>
        {
            Assert.That(memory.Kind, Is.EqualTo("memory"));
            Assert.That(memory.Configuration["value"].GetBoolean(), Is.False);
            Assert.That(result.Flow.Nodes.Single(node => node.Id == "output-01-node")
                .Configuration["pointId"].GetString(), Is.EqualTo("output-01"));
            Assert.That(result.Flow.Connections.Any(connection =>
                connection.Start.NodeId == "or-1"
                && connection.End == new FlowEndpoint("memory-1", "in")), Is.True);
        });
    }

    [Test]
    public void RejectsCorruptArtifactsBeforeReadingInstructions()
    {
        var artifact = Artifact("valid-two-button-and");
        artifact[^1] ^= 1;

        var exception = Assert.Throws<FlowDecompilationException>(
            () => new FlowDecompiler().Decompile(artifact));

        Assert.That(exception!.Diagnostic.Code, Is.EqualTo("invalid_digest"));
    }

    [Test]
    public void RejectsUnsupportedArtifactVersionsWithAStablePath()
    {
        var artifact = Artifact("valid-two-button-and");
        artifact[4] = 3;

        var exception = Assert.Throws<FlowDecompilationException>(
            () => new FlowDecompiler().Decompile(artifact));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Diagnostic.Code, Is.EqualTo("unsupported_version"));
            Assert.That(exception.Diagnostic.Path, Is.EqualTo("/version"));
        });
    }

    private static byte[] Artifact(string fixture) =>
        File.ReadAllBytes(Path.Combine(FixtureRoot, fixture, "artifact.bin"));

    private static FlowCompilationRequest CompilationRequest(ExecutableFlowSource source) => new()
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
}
