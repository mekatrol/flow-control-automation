using Server.Services;
using Server.Services.Contracts;
using Server.Services.Implementation;
using System.Text.Json;

namespace Tests.Unit.Flows;

public sealed class FlowCompilerTests
{
    private static readonly string FixtureRoot = Path.Combine(
        AppContext.BaseDirectory,
        "ContractFixtures",
        "flow-executable-v1");

    [TestCase("valid-two-button-and")]
    [TestCase("valid-source-order-permutation")]
    [TestCase("valid-memory-feedback")]
    public void CompilesGoldenSourceToTheExactCanonicalArtifact(string fixture)
    {
        var source = ReadSource(fixture);

        var result = new FlowCompiler().Compile(Request(source));
        var expected = File.ReadAllBytes(Path.Combine(FixtureRoot, fixture, "artifact.bin"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Artifact.ToArray(), Is.EqualTo(expected));
            Assert.That(result.ArtifactSha256,
                Is.EqualTo(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(expected))));
            Assert.That(result.NodeIndices.Keys,
                Is.EqualTo(source.Nodes.Select(node => node.Id).Order(StringComparer.Ordinal)));
        });
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
        var source = ReadSource("combinational-cycle");

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

    private static ExecutableFlowSource ReadSource(string fixture)
    {
        var json = File.ReadAllText(Path.Combine(FixtureRoot, fixture, "source-flow.json"));
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
                new HashSet<ControllerRuntimeFeature>())
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
