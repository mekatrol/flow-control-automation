using System.Net;
using System.Net.Http.Json;
using Server.Services.Contracts;

namespace Tests.Unit.Api;

[TestFixture]
public sealed class FlowScenarioEndpointTests
{
    /// <summary>
    /// Purpose: Proves scenarios are persisted independently and can be listed for their flow.
    /// Description: Saves a bounded schema-version-one scenario, then reads the flow collection.
    /// </summary>
    [Test]
    public async Task SaveAndListPersistScenario()
    {
        // Arrange: Create an isolated API and a deterministic scenario document.
        await using var factory = new FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        var scenario = Scenario();

        // Act: Persist through the public contract and retrieve the flow's scenarios.
        var save = await client.PutAsJsonAsync("/api/flows/flow-1/scenarios/scenario-1", scenario);
        var list = await client.GetFromJsonAsync<List<FlowScenario>>("/api/flows/flow-1/scenarios");

        // Assert: The stored document remains separate and retains stable identifiers.
        Assert.Multiple(() =>
        {
            Assert.That(save.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(list, Has.Count.EqualTo(1));
            Assert.That(list![0].Id, Is.EqualTo("scenario-1"));
            Assert.That(list[0].FlowId, Is.EqualTo("flow-1"));
        });
    }

    /// <summary>
    /// Purpose: Protects strict current-version parsing at the API boundary.
    /// Description: Attempts to save a scenario with a superseded or future schema version.
    /// </summary>
    [Test]
    public async Task SaveRejectsUnsupportedSchemaVersion()
    {
        // Arrange: Build a document whose explicit schema version is unsupported.
        await using var factory = new FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        var scenario = Scenario() with { SchemaVersion = 2 };

        // Act: Submit the invalid document through the persistence endpoint.
        var response = await client.PutAsJsonAsync("/api/flows/flow-1/scenarios/scenario-1", scenario);

        // Assert: The contract rejects it rather than adding a compatibility path.
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
    }

    private static FlowScenario Scenario() => new()
    {
        Id = "scenario-1",
        Name = "Boolean example",
        FlowId = "flow-1",
        FlowRevision = 7,
        Steps =
        [
            new FlowScenarioStep
            {
                AtMilliseconds = 0,
                Action = "step",
                Inputs = [new EmulatorInputChange("input-1", FlowVmValue.FromBoolean(true))]
            }
        ],
        Expectations =
        [
            new FlowScenarioExpectation
            {
                OutputId = "output-1",
                Operator = "equals",
                ExpectedValue = FlowVmValue.FromBoolean(true)
            }
        ]
    };
}
