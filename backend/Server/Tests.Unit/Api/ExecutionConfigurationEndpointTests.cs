using Server.Common.Contracts;
using Server.Services.Contracts;
using System.Net;
using System.Net.Http.Json;

namespace Tests.Unit.Api;

[TestFixture]
internal sealed class ExecutionConfigurationEndpointTests
{
    [Test]
    public async Task ContextDeploysToTwoInstancesWithIsolatedAllocations()
    {
        await using var factory = new FlowControlApplicationFactory();
        using var client = factory.CreateClient();

        var serverInstances = await client.GetFromJsonAsync<List<ExecutionInstance>>("/api/execution-instances", FlowControlJson.Options);
        Assert.That(serverInstances, Has.Count.EqualTo(1));
        Assert.That(serverInstances![0].Id, Is.EqualTo("server"));

        var createdFlowResponse = await client.PostAsJsonAsync("/api/flows", new { name = "Virtual writer" });
        var flow = (await createdFlowResponse.Content.ReadFromJsonAsync<Flow>(FlowControlJson.Options))! with
        {
            VirtualPointDeclarations =
            [
                new VirtualPointDeclaration
                {
                    Key = "temp-setpoint",
                    ValueType = FlowPointValueType.Analog,
                    Units = "degC",
                    Readable = true,
                    Commandable = true,
                    Persistence = VirtualPointPersistence.Retained
                }
            ]
        };
        var savedResponse = await client.PutAsJsonAsync($"/api/flows/{flow.Id}", flow, FlowControlJson.Options);
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

        foreach (var instanceId in new[] { "east", "west" })
        {
            var instanceResponse = await client.PostAsJsonAsync("/api/execution-instances", new ExecutionInstance
            {
                Id = instanceId,
                Name = instanceId,
                Kind = ExecutionInstanceKind.Controller,
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
                Status = ExecutionContextDeploymentStatus.Active
            }, FlowControlJson.Options);
            Assert.That(deploymentResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
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
        });
    }
}