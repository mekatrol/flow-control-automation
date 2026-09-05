using Server.Common.Models;
using Server.Common.Types;
using Server.Services.Contracts;
using System.Net;
using System.Net.Http.Json;

namespace Tests.Unit.Api;

[TestFixture]
internal sealed class ExecutionConfigurationEndpointTests
{
    [Test]
    public async Task SavingFlowReconcilesEveryContainingContextAtomically()
    {
        await using var factory = new FlowControlApplicationFactory();
        using var client = factory.CreateClient();

        var createdFlowResponse = await client.PostAsJsonAsync("/api/flows", new { name = "Shared declarations" });
        var flow = (await createdFlowResponse.Content.ReadFromJsonAsync<Flow>(FlowControlJson.Options))!;
        foreach (var contextId in new[] { "first-context", "second-context" })
        {
            var response = await client.PostAsJsonAsync("/api/execution-contexts", new ExecutionContextDefinition
            {
                Id = contextId,
                Name = contextId,
                Programs = [new(flow.Id, flow.Revision)]
            }, FlowControlJson.Options);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        }

        var saveResponse = await client.PutAsJsonAsync($"/api/flows/{flow.Id}", flow with
        {
            Nodes =
            [
                new FlowNode
                {
                    Id = "shared-temperature",
                    NodeType = FlowNodeType.AnalogVirtual,
                    Label = "Shared temperature",
                    Connectors = [new FlowConnector("value", "Value", DataDirectionType.Output, DataType.Number, "right")],
                    Configuration = new Dictionary<string, System.Text.Json.JsonElement>
                    {
                        ["pointId"] = System.Text.Json.JsonSerializer.SerializeToElement("shared-temperature"),
                        ["persistence"] = System.Text.Json.JsonSerializer.SerializeToElement("volatile")
                    }
                }
            ]
        }, FlowControlJson.Options);
        Assert.That(saveResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK), await saveResponse.Content.ReadAsStringAsync());
        var saved = (await saveResponse.Content.ReadFromJsonAsync<Flow>(FlowControlJson.Options))!;

        foreach (var contextId in new[] { "first-context", "second-context" })
        {
            var definition = await client.GetFromJsonAsync<ExecutionContextDefinition>($"/api/execution-contexts/{contextId}", FlowControlJson.Options);
            Assert.Multiple(() =>
            {
                Assert.That(definition!.Programs.Single().FlowRevision, Is.EqualTo(saved.Revision));
                Assert.That(definition.PointContracts.Single().Key, Is.EqualTo("shared-temperature"));
                Assert.That(definition.Revision, Is.EqualTo(2));
            });
        }
    }

    [Test]
    public async Task ContextDeploysToTwoInstancesWithIsolatedAllocations()
    {
        await using var factory = new FlowControlApplicationFactory();
        using var client = factory.CreateClient();

        var serverInstances = await client.GetFromJsonAsync<List<ExecutionInstance>>("/api/execution-instances", FlowControlJson.Options);
        Assert.That(serverInstances, Has.Count.EqualTo(1));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(serverInstances![0].Id, Is.EqualTo("server"));
            Assert.That(serverInstances[0].ExecutionInstanceType, Is.EqualTo(ExecutionInstanceType.Server));
        }

        var createdFlowResponse = await client.PostAsJsonAsync("/api/flows", new { name = "Virtual writer" });
        var flow = (await createdFlowResponse.Content.ReadFromJsonAsync<Flow>(FlowControlJson.Options))! with
        {
            Nodes =
            [
                new FlowNode
                {
                    Id = "constant",
                    NodeType = FlowNodeType.AnalogConstant,
                    Label = "Setpoint",
                    Connectors = [new FlowConnector("value", "Value", DataDirectionType.Output, DataType.Number, "right")],
                    Configuration = new Dictionary<string, System.Text.Json.JsonElement>
                    {
                        ["value"] = System.Text.Json.JsonSerializer.SerializeToElement(21.5)
                    }
                },
                new FlowNode
                {
                    Id = "output",
                    NodeType = FlowNodeType.AnalogOutput,
                    Label = "Temperature setpoint",
                    Connectors = [new FlowConnector("in", "Input", DataDirectionType.Input, DataType.Number, "left")],
                    Configuration = new Dictionary<string, System.Text.Json.JsonElement>
                    {
                        ["pointId"] = System.Text.Json.JsonSerializer.SerializeToElement("temp-setpoint")
                    }
                },
                new FlowNode
                {
                    Id = "virtual",
                    NodeType = FlowNodeType.AnalogVirtual,
                    Label = "Virtual temperature",
                    Connectors = [new FlowConnector("value", "Value", DataDirectionType.Output, DataType.Number, "right")],
                    Configuration = new Dictionary<string, System.Text.Json.JsonElement>
                    {
                        ["pointId"] = System.Text.Json.JsonSerializer.SerializeToElement("temp-setpoint"),
                        ["persistence"] = System.Text.Json.JsonSerializer.SerializeToElement("retained")
                    }
                }
            ],
            Connections = [new FlowConnection("write", new FlowEndpoint("constant", "value"), new FlowEndpoint("output", "in"))]
        };
        var savedResponse = await client.PutAsJsonAsync($"/api/flows/{flow.Id}", flow, FlowControlJson.Options);
        Assert.That(savedResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK), await savedResponse.Content.ReadAsStringAsync());
        var saved = (await savedResponse.Content.ReadFromJsonAsync<Flow>(FlowControlJson.Options))!;

        var definition = new ExecutionContextDefinition
        {
            Id = "climate",
            Name = "Climate",
            Programs = [new(saved.Id, saved.Revision)]
        };
        var contextResponse = await client.PostAsJsonAsync("/api/execution-contexts", definition, FlowControlJson.Options);
        Assert.That(contextResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var createdContext = (await contextResponse.Content.ReadFromJsonAsync<ExecutionContextDefinition>(FlowControlJson.Options))!;
        Assert.That(createdContext.PointContracts, Has.Count.EqualTo(1));

        var resolution = await client.GetFromJsonAsync<PointResolution>(
            "/api/point-resolution/temp-setpoint?executionContextId=climate&executionInstanceId=server",
            FlowControlJson.Options);
        Assert.Multiple(() =>
        {
            Assert.That(resolution!.Exists, Is.True);
            Assert.That(resolution.PointSourceType, Is.EqualTo(PointSourceType.Virtual));
            Assert.That(resolution.ValueType, Is.EqualTo(AutomationPointValueType.Analog));
            Assert.That(resolution.ExecutionContextId, Is.EqualTo("climate"));
            Assert.That(resolution.ExecutionInstanceId, Is.EqualTo("server"));
        });

        var missingResolution = await client.GetFromJsonAsync<PointResolution>(
            "/api/point-resolution/missing?executionContextId=climate",
            FlowControlJson.Options);
        Assert.That(missingResolution!.Exists, Is.False);

        var deployments = new List<ExecutionContextDeployment>();
        foreach (var instanceId in new[] { "east", "west" })
        {
            var instanceResponse = await client.PostAsJsonAsync("/api/execution-instances", new ExecutionInstance
            {
                Id = instanceId,
                Name = instanceId,
                ExecutionInstanceType = ExecutionInstanceType.Controller,
                ControllerTemplateId = "default",
                ControllerTemplateRevision = 1
            }, FlowControlJson.Options);
            Assert.That(instanceResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            var deploymentResponse = await client.PostAsJsonAsync("/api/execution-contexts/climate/deployments", new ExecutionContextDeployment
            {
                Id = $"climate-{instanceId}",
                ExecutionContextId = "climate",
                ExecutionContextRevision = createdContext.Revision,
                ExecutionInstanceId = instanceId,
                Status = ExecutionContextDeploymentStatusType.Active
            }, FlowControlJson.Options);
            Assert.That(deploymentResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created), await deploymentResponse.Content.ReadAsStringAsync());
            deployments.Add((await deploymentResponse.Content.ReadFromJsonAsync<ExecutionContextDeployment>(FlowControlJson.Options))!);
        }

        var east = await client.GetFromJsonAsync<List<VirtualPointAllocation>>("/api/execution-instances/east/virtual-points", FlowControlJson.Options);
        var west = await client.GetFromJsonAsync<List<VirtualPointAllocation>>("/api/execution-instances/west/virtual-points", FlowControlJson.Options);
        Assert.Multiple(() =>
        {
            Assert.That(east, Has.Count.EqualTo(1));
            Assert.That(west, Has.Count.EqualTo(1));
            Assert.That(east![0].ExecutionInstanceId, Is.EqualTo("east"));
            Assert.That(west![0].ExecutionInstanceId, Is.EqualTo("west"));
            Assert.That(east[0].PointKey, Is.EqualTo("temp-setpoint"));
            Assert.That(west[0].PointKey, Is.EqualTo("temp-setpoint"));
            Assert.That(deployments, Has.All.Matches<ExecutionContextDeployment>(item => item.CompiledPrograms.Count == 1));
            Assert.That(deployments[0].CompiledPrograms[0].ExecutionInstanceId, Is.EqualTo("east"));
            Assert.That(deployments[1].CompiledPrograms[0].ExecutionInstanceId, Is.EqualTo("west"));
            Assert.That(deployments[0].CompiledPrograms[0].ExecutionContextId, Is.EqualTo("climate"));
            Assert.That(deployments[0].CompiledPrograms[0].ArtifactBase64, Is.Not.Empty);
            Assert.That(deployments[0].CompiledPrograms[0].ArtifactSha256, Has.Length.EqualTo(64));
        });
    }
}