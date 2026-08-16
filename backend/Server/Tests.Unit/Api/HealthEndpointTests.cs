using System.Net;
using System.Net.Http.Json;

namespace Tests.Unit.Api;

public sealed class HealthEndpointTests
{
    /// <summary>
    /// Purpose: Protects the health endpoint contract used by monitors to determine whether the API is available.
    /// Description: Requests the health resource and verifies its success status, JSON media type, and healthy payload.
    /// </summary>
    [Test]
    public async Task HealthReturnsCompatibleJson()
    {
        await using var factory = new FlowControlApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/health");

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // health returns compatible json.
        Assert.Multiple(() =>
        {
            // Expected outcome: The health request succeeds.
            // Acceptance criteria: The response is HTTP 200 OK because a running application must expose an available health resource to monitoring clients.
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            // Expected outcome: The health response is identified as JSON.
            // Acceptance criteria: The media type is application/json because clients must be able to deserialize the endpoint's documented JSON health contract.
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));
        });
        var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();

        // Expected outcome: The health payload reports that the application is healthy.
        // Acceptance criteria: The payload contains exactly status "ok" because that is the endpoint contract monitors use to recognise a healthy application.
        Assert.That(payload, Is.EquivalentTo(new Dictionary<string, string> { ["status"] = "ok" }));
    }
}