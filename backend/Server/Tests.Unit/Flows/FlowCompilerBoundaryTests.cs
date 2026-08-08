using Server.Services;
using Server.Services.Contracts;
using System.Text.Json;

namespace Tests.Unit.Flows;

public sealed class FlowCompilerBoundaryTests
{
    private static readonly string FixtureRoot = Path.Combine(
        AppContext.BaseDirectory,
        "ContractFixtures",
        "flow-executable-v1");

    [Test]
    public void CompilationFailureRequiresStructuredDiagnostics()
    {
        var diagnostics = new[]
        {
            new FlowCompilationDiagnostic(
                "unsupported_node",
                "/nodes/example",
                "The node is outside executable schema 1.")
        };

        var exception = new FlowCompilationException(diagnostics);

        Assert.Multiple(() =>
        {
            Assert.That(exception.Diagnostics, Is.SameAs(diagnostics));
            Assert.That(exception.Message,
                Is.EqualTo("Flow compilation failed: unsupported_node at /nodes/example"));
        });
    }

    [Test]
    public void CompilationFailureRejectsAnEmptyDiagnosticCollection()
    {
        Assert.That(
            () => new FlowCompilationException([]),
            Throws.ArgumentException.With.Property("ParamName").EqualTo("diagnostics"));
    }

    [Test]
    public void CompilerBoundaryAcceptsResolvedInputWithoutPersistenceOrTransportDependencies()
    {
        var method = typeof(IFlowCompiler).GetMethod(nameof(IFlowCompiler.Compile));

        Assert.Multiple(() =>
        {
            Assert.That(method, Is.Not.Null);
            Assert.That(method!.ReturnType, Is.EqualTo(typeof(FlowCompilationResult)));
            Assert.That(method.GetParameters().Single().ParameterType,
                Is.EqualTo(typeof(FlowCompilationRequest)));
            Assert.That(typeof(FlowCompilationRequest).GetProperty(nameof(FlowCompilationRequest.Target)),
                Is.Not.Null);
            Assert.That(typeof(FlowCompilationRequest).GetProperty(nameof(FlowCompilationRequest.Source))!
                .PropertyType, Is.EqualTo(typeof(ExecutableFlowSource)));
        });
    }

    [Test]
    public void GoldenSourceFlowDeserializesIntoTheCompilerSourceContract()
    {
        var json = File.ReadAllText(Path.Combine(
            FixtureRoot,
            "valid-two-button-and",
            "source-flow.json"));

        var source = JsonSerializer.Deserialize<ExecutableFlowSource>(
            json,
            FlowControlJson.Options);

        Assert.Multiple(() =>
        {
            Assert.That(source, Is.Not.Null);
            Assert.That(source!.SchemaVersion, Is.EqualTo(1));
            Assert.That(source.Id, Is.EqualTo("two-button-and"));
            Assert.That(source.Revision, Is.EqualTo(7));
            Assert.That(source.Execution.Mode, Is.EqualTo("manual"));
            Assert.That(source.Nodes, Has.Count.EqualTo(4));
            Assert.That(source.Connections, Has.Count.EqualTo(3));
        });
    }
}
