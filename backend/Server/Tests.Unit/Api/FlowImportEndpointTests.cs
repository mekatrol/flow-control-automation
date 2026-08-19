using Server.Api.Contracts;
using Server.Services.Contracts;
using System.Net;
using System.Net.Http.Json;

namespace Tests.Unit.Api;

[TestFixture]
internal sealed class FlowImportEndpointTests
{
    private static readonly string FixtureRoot = Path.Combine(
        AppContext.BaseDirectory,
        "ContractFixtures",
        "flow-il-v1");

    [Test]
    public async Task PreviewDoesNotPersistAndReturnsRecoveryProvenance()
    {
        await using var factory = new FlowControlApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/flows/import-il",
            Request("valid-two-button-and", "Recovered preview", save: false),
            FlowControlJson.Options);
        var result = await response.Content.ReadFromJsonAsync<ImportFlowIlResponse>(FlowControlJson.Options);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Saved, Is.False);
            Assert.That(result.Flow.Name, Is.EqualTo("Recovered preview"));
            Assert.That(result.Flow.Nodes, Has.Count.EqualTo(4));
            Assert.That(result.Provenance.ArtifactVersion, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task SaveCreatesANewDraftWithoutOverwritingTheArtifactFlowId()
    {
        await using var factory = new FlowControlApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/flows/import-il",
            Request("valid-memory-feedback", "Imported feedback", save: true),
            FlowControlJson.Options);

        var result = await response.Content.ReadFromJsonAsync<ImportFlowIlResponse>(FlowControlJson.Options);

        var loaded = await client.GetFromJsonAsync<Flow>(
            $"/api/flows/{result!.Flow.Id}",
            FlowControlJson.Options);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(result.Saved, Is.True);
            Assert.That(result.Flow.Id, Is.EqualTo("imported-feedback"));
            Assert.That(loaded!.Nodes, Has.Count.EqualTo(3));
            Assert.That(loaded.Connections, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task CorruptArtifactReturnsStructuredUnprocessableEntity()
    {
        await using var factory = new FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        var artifact = File.ReadAllBytes(Path.Combine(FixtureRoot, "valid-two-button-and", "artifact.bin"));
        artifact[^1] ^= 1;

        using var response = await client.PostAsJsonAsync(
            "/api/flows/import-il",
            new ImportFlowIlRequest { ArtifactBase64 = Convert.ToBase64String(artifact) },
            FlowControlJson.Options);
        var diagnostic = await response.Content.ReadFromJsonAsync<FlowCompilationDiagnostic>(FlowControlJson.Options);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
            Assert.That(diagnostic!.Code, Is.EqualTo(FlowCompilerCode.InvalidDigest));
        });
    }

    private static ImportFlowIlRequest Request(string fixture, string name, bool save) => new()
    {
        ArtifactBase64 = Convert.ToBase64String(
            File.ReadAllBytes(Path.Combine(FixtureRoot, fixture, "artifact.bin"))),
        Name = name,
        Save = save
    };
}