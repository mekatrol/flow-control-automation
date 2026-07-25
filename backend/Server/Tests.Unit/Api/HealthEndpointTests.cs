using System.Net;
using System.Net.Http.Json;

namespace Tests.Unit.Api;

public sealed class HealthEndpointTests
{
    [Test]
    public async Task HealthReturnsCompatibleJson()
    {
        using var factory = new FlowControlApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/health");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));
        });
        var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.That(payload, Is.EquivalentTo(new Dictionary<string, string> { ["status"] = "ok" }));
    }
}