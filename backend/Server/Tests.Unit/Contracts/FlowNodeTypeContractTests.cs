using Server.Common.Models;
using Server.Common.Types;
using Server.Services.Contracts;
using System.Text.Json;

namespace Tests.Unit.Contracts;

public sealed class FlowNodeTypeContractTests
{
    [Test]
    public void DesignerAndExecutableNodesUseNodeTypeOnTheWire()
    {
        var designer = new FlowNode { Id = "node", NodeType = FlowNodeType.A2D };
        var executable = new ExecutableFlowNode { Id = "node", NodeType = FlowNodeType.A2D };
        using var designerJson = JsonDocument.Parse(JsonSerializer.Serialize(designer, FlowControlJson.Options));
        using var executableJson = JsonDocument.Parse(JsonSerializer.Serialize(executable, FlowControlJson.Options));
        Assert.Multiple(() =>
        {
            Assert.That(designerJson.RootElement.GetProperty("nodeType").GetString(), Is.EqualTo("a2d"));
            Assert.That(executableJson.RootElement.GetProperty("nodeType").GetString(), Is.EqualTo("a2d"));
            Assert.That(designerJson.RootElement.TryGetProperty("kind", out _), Is.False);
            Assert.That(executableJson.RootElement.TryGetProperty("kind", out _), Is.False);
            Assert.That(JsonSerializer.Deserialize<FlowNode>(designerJson.RootElement, FlowControlJson.Options)!.NodeType, Is.EqualTo(FlowNodeType.A2D));
            Assert.That(JsonSerializer.Deserialize<ExecutableFlowNode>(executableJson.RootElement, FlowControlJson.Options)!.NodeType, Is.EqualTo(FlowNodeType.A2D));
        });
    }

    [Test]
    public void ApiContractsRejectTheOldKindField()
    {
        const string legacy = """{"id":"node","kind":"analogInput"}""";
        Assert.Multiple(() =>
        {
            Assert.That(() => JsonSerializer.Deserialize<FlowNode>(legacy, FlowControlJson.Options), Throws.TypeOf<JsonException>());
            Assert.That(() => JsonSerializer.Deserialize<ExecutableFlowNode>(legacy, FlowControlJson.Options), Throws.TypeOf<JsonException>());
        });
    }
}
