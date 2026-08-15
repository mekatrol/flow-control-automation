using Server.Api.Contracts;
using Server.Data.Context;
using Server.Services;
using Server.Services.Contracts;
using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace Tests.Unit.Credentials;

[TestFixture]
internal sealed class CredentialEndpointTests
{

    /// <summary>
    /// Purpose: Protects the behavioral contract that store encrypts secrets and resolver survives scope restart.
    /// Description: Arranges the inputs for store encrypts secrets and resolver survives scope restart, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public async Task StoreEncryptsSecretsAndResolverSurvivesScopeRestart()
    {
        await using var factory = new Api.FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        var input = MqttCredential();
        using var response = await client.PostAsJsonAsync(
            "/api/credentials",
            input,
            FlowControlJson.Options);
        var body = await response.Content.ReadAsStringAsync();
        var metadata = System.Text.Json.JsonSerializer.Deserialize<CredentialMetadata>(
            body,
            FlowControlJson.Options);

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // store encrypts secrets and resolver survives scope restart.
        Assert.Multiple(() =>
        {

            // Expected outcome: `response.StatusCode` has the required value.
            // Acceptance criteria: `response.StatusCode` must equal `HttpStatusCode.Created`, because this condition proves that
            // store encrypts secrets and resolver survives scope restart.
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

            // Expected outcome: `metadata!.Username` has the required value.
            // Acceptance criteria: `metadata!.Username` must equal `"reader"`, because this condition proves that
            // store encrypts secrets and resolver survives scope restart.
            Assert.That(metadata!.Username, Is.EqualTo("reader"));

            // Expected outcome: `metadata.Revision` has the required value.
            // Acceptance criteria: `metadata.Revision` must equal `1`, because this condition proves that
            // store encrypts secrets and resolver survives scope restart.
            Assert.That(metadata.Revision, Is.EqualTo(1));

            // Expected outcome: `body.Contains("highly-secret"` rejects the prohibited condition.
            // Acceptance criteria: `body.Contains("highly-secret"` must be false, because this condition proves that
            // store encrypts secrets and resolver survives scope restart.
            Assert.That(
                body.Contains("highly-secret", StringComparison.Ordinal),
                Is.False,
                "API response contains plaintext credential material");

            // Expected outcome: The observed result satisfies the required contract.
            // Acceptance criteria: the asserted condition must hold, because this condition proves that
            // store encrypts secrets and resolver survives scope restart.
            Assert.That(body, Does.Not.Contain("\"password\""));

            // Expected outcome: The observed result satisfies the required contract.
            // Acceptance criteria: the asserted condition must hold, because this condition proves that
            // store encrypts secrets and resolver survives scope restart.
            Assert.That(body, Does.Not.Contain("\"token\""));
        });

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IFlowControlDbContext>();
            var row = await context.Credentials.AsNoTracking().SingleAsync();

            // Expected outcome: All related outcomes satisfy their contracts.
            // Acceptance criteria: every assertion in the group must pass, because this condition proves that
            // store encrypts secrets and resolver survives scope restart.
            Assert.Multiple(() =>
            {

                // Expected outcome: `row.Json.Contains("highly-secret"` rejects the prohibited condition.
                // Acceptance criteria: `row.Json.Contains("highly-secret"` must be false, because this condition proves that
                // store encrypts secrets and resolver survives scope restart.
                Assert.That(
                    row.Json.Contains("highly-secret", StringComparison.Ordinal),
                    Is.False,
                    "database row contains plaintext credential material");

                // Expected outcome: `row.Json` includes the required content.
                // Acceptance criteria: `row.Json` must contain `"\"secret\""`, because this condition proves that
                // store encrypts secrets and resolver survives scope restart.
                Assert.That(row.Json, Does.Contain("\"secret\""));
            });

            var resolver = scope.ServiceProvider.GetRequiredService<ICredentialResolver>();
            var resolved = await resolver.ResolveAsync(
                "secret://plant-mqtt",
                CancellationToken.None);

            // Expected outcome: All related outcomes satisfy their contracts.
            // Acceptance criteria: every assertion in the group must pass, because this condition proves that
            // store encrypts secrets and resolver survives scope restart.
            Assert.Multiple(() =>
            {

                // Expected outcome: `resolved` includes the required content.
                // Acceptance criteria: `resolved` must contain `"\"username\":\"reader\""`, because this condition proves that
                // store encrypts secrets and resolver survives scope restart.
                Assert.That(resolved, Does.Contain("\"username\":\"reader\""));

                // Expected outcome: the asserted result confirms the required condition.
                // Acceptance criteria: the asserted result must be true, because this condition proves that
                // store encrypts secrets and resolver survives scope restart.
                Assert.That(
                    resolved.Contains(
                        "\"password\":\"highly-secret\"",
                        StringComparison.Ordinal),
                    Is.True,
                    "resolved MQTT credential does not contain its password");
            });
        }

        await using (var restartedScope = factory.Services.CreateAsyncScope())
        {
            var resolver =
                restartedScope.ServiceProvider.GetRequiredService<ICredentialResolver>();
            var resolved = await resolver.ResolveAsync(
                "secret://plant-mqtt",
                CancellationToken.None);

            // Expected outcome: `resolved.Contains("highly-secret"` confirms the required condition.
            // Acceptance criteria: `resolved.Contains("highly-secret"` must be true, because this condition proves that
            // store encrypts secrets and resolver survives scope restart.
            Assert.That(
                resolved.Contains("highly-secret", StringComparison.Ordinal),
                Is.True,
                "credential did not resolve after creating a new scope");
        }
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that crud lists metadata and preserves secret when update omits it.
    /// Description: Arranges the inputs for crud lists metadata and preserves secret when update omits it, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public async Task CrudListsMetadataAndPreservesSecretWhenUpdateOmitsIt()
    {
        await using var factory = new Api.FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        var input = TokenCredential();
        var created = await Create(client, input);

        using var get = await client.GetAsync("/api/credentials/weather-token");
        var getBody = await get.Content.ReadAsStringAsync();
        var loaded = System.Text.Json.JsonSerializer.Deserialize<CredentialMetadata>(
            getBody,
            FlowControlJson.Options);
        var changed = input with
        {
            Name = "Updated weather token",
            Token = null,
            Revision = created.Revision,
        };
        using var update = await client.PutAsJsonAsync(
            "/api/credentials/weather-token",
            changed,
            FlowControlJson.Options);
        var updated = await update.Content.ReadFromJsonAsync<CredentialMetadata>(
            FlowControlJson.Options);
        var list = await client.GetFromJsonAsync<CredentialListResponse>(
            "/api/credentials",
            FlowControlJson.Options);

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // crud lists metadata and preserves secret when update omits it.
        Assert.Multiple(() =>
        {

            // Expected outcome: `get.StatusCode` has the required value.
            // Acceptance criteria: `get.StatusCode` must equal `HttpStatusCode.OK`, because this condition proves that
            // crud lists metadata and preserves secret when update omits it.
            Assert.That(get.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            // Expected outcome: `loaded!.Name` has the required value.
            // Acceptance criteria: `loaded!.Name` must equal `"Weather token"`, because this condition proves that
            // crud lists metadata and preserves secret when update omits it.
            Assert.That(loaded!.Name, Is.EqualTo("Weather token"));

            // Expected outcome: The observed result satisfies the required contract.
            // Acceptance criteria: the asserted condition must hold, because this condition proves that
            // crud lists metadata and preserves secret when update omits it.
            Assert.That(getBody, Does.Not.Contain("weather-secret"));

            // Expected outcome: `update.StatusCode` has the required value.
            // Acceptance criteria: `update.StatusCode` must equal `HttpStatusCode.OK`, because this condition proves that
            // crud lists metadata and preserves secret when update omits it.
            Assert.That(update.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            // Expected outcome: `updated!.Revision` has the required value.
            // Acceptance criteria: `updated!.Revision` must equal `2`, because this condition proves that
            // crud lists metadata and preserves secret when update omits it.
            Assert.That(updated!.Revision, Is.EqualTo(2));

            // Expected outcome: `updated.Name` has the required value.
            // Acceptance criteria: `updated.Name` must equal `"Updated weather token"`, because this condition proves that
            // crud lists metadata and preserves secret when update omits it.
            Assert.That(updated.Name, Is.EqualTo("Updated weather token"));

            // Expected outcome: `list!.Items` contains the required number of entries.
            // Acceptance criteria: `list!.Items` must contain exactly 1 entries, because this condition proves that
            // crud lists metadata and preserves secret when update omits it.
            Assert.That(list!.Items, Has.Count.EqualTo(1));

            // Expected outcome: `list.Items[0].Revision` has the required value.
            // Acceptance criteria: `list.Items[0].Revision` must equal `2`, because this condition proves that
            // crud lists metadata and preserves secret when update omits it.
            Assert.That(list.Items[0].Revision, Is.EqualTo(2));
        });

        await using var scope = factory.Services.CreateAsyncScope();
        var resolver = scope.ServiceProvider.GetRequiredService<ICredentialResolver>();
        var resolved = await resolver.ResolveAsync(
            "secret://weather-token",
            CancellationToken.None);

        // Expected outcome: `resolved` has the required value.
        // Acceptance criteria: `resolved` must equal `"weather-secret"`, because this condition proves that
        // crud lists metadata and preserves secret when update omits it.
        Assert.That(
            resolved,
            Is.EqualTo("weather-secret"),
            "updating metadata changed the stored secret");
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that stale revision mismatched id and duplicate name conflict.
    /// Description: Arranges the inputs for stale revision mismatched id and duplicate name conflict, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public async Task StaleRevisionMismatchedIdAndDuplicateNameConflict()
    {
        await using var factory = new Api.FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        var first = TokenCredential();
        var created = await Create(client, first);
        await Create(
            client,
            first with
            {
                Id = "second-token",
                Name = "Second token",
                Token = "second-secret",
            });

        using var stale = await client.PutAsJsonAsync(
            "/api/credentials/weather-token",
            first with { Revision = created.Revision + 1 },
            FlowControlJson.Options);
        using var mismatch = await client.PutAsJsonAsync(
            "/api/credentials/weather-token",
            first with { Id = "different", Revision = created.Revision },
            FlowControlJson.Options);
        using var duplicateName = await client.PutAsJsonAsync(
            "/api/credentials/weather-token",
            first with { Name = "SECOND TOKEN", Revision = created.Revision },
            FlowControlJson.Options);
        using var staleDelete = await client.DeleteAsync(
            "/api/credentials/weather-token?revision=2");

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // stale revision mismatched id and duplicate name conflict.
        Assert.Multiple(() =>
        {

            // Expected outcome: `stale.StatusCode` has the required value.
            // Acceptance criteria: `stale.StatusCode` must equal `HttpStatusCode.Conflict`, because this condition proves that
            // stale revision mismatched id and duplicate name conflict.
            Assert.That(stale.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));

            // Expected outcome: `mismatch.StatusCode` has the required value.
            // Acceptance criteria: `mismatch.StatusCode` must equal `HttpStatusCode.Conflict`, because this condition proves that
            // stale revision mismatched id and duplicate name conflict.
            Assert.That(mismatch.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));

            // Expected outcome: `duplicateName.StatusCode` has the required value.
            // Acceptance criteria: `duplicateName.StatusCode` must equal `HttpStatusCode.Conflict`, because this condition proves that
            // stale revision mismatched id and duplicate name conflict.
            Assert.That(duplicateName.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));

            // Expected outcome: `staleDelete.StatusCode` has the required value.
            // Acceptance criteria: `staleDelete.StatusCode` must equal `HttpStatusCode.Conflict`, because this condition proves that
            // stale revision mismatched id and duplicate name conflict.
            Assert.That(staleDelete.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        });
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that delete is blocked while point source references credential.
    /// Description: Arranges the inputs for delete is blocked while point source references credential, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public async Task DeleteIsBlockedWhilePointSourceReferencesCredential()
    {
        await using var factory = new Api.FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        var credential = await Create(client, TokenCredential());
        var source = ValidHttpSource() with
        {
            CredentialRef = "secret://weather-token",
        };
        using var sourceResponse = await client.PostAsync(
            "/api/point-sources",
            new StringContent(
                PointSourceYaml.Render(source),
                Encoding.UTF8,
                "application/yaml"));

        // Expected outcome: `sourceResponse.StatusCode` has the required value.
        // Acceptance criteria: `sourceResponse.StatusCode` must equal `HttpStatusCode.Created`, because this condition proves that
        // delete is blocked while point source references credential.
        Assert.That(sourceResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        using var blocked = await client.DeleteAsync(
            $"/api/credentials/weather-token?revision={credential.Revision}");
        var error = await blocked.Content.ReadFromJsonAsync<Dictionary<string, string>>();

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // delete is blocked while point source references credential.
        Assert.Multiple(() =>
        {

            // Expected outcome: `blocked.StatusCode` has the required value.
            // Acceptance criteria: `blocked.StatusCode` must equal `HttpStatusCode.Conflict`, because this condition proves that
            // delete is blocked while point source references credential.
            Assert.That(blocked.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));

            // Expected outcome: `error!["message"]` includes the required content.
            // Acceptance criteria: `error!["message"]` must contain `"\"weather\""`, because this condition proves that
            // delete is blocked while point source references credential.
            Assert.That(error!["message"], Does.Contain("\"weather\""));
        });

        using var deleteSource = await client.DeleteAsync(
            "/api/point-sources/weather?revision=1");

        // Expected outcome: `deleteSource.StatusCode` has the required value.
        // Acceptance criteria: `deleteSource.StatusCode` must equal `HttpStatusCode.NoContent`, because this condition proves that
        // delete is blocked while point source references credential.
        Assert.That(deleteSource.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        using var deleted = await client.DeleteAsync(
            $"/api/credentials/weather-token?revision={credential.Revision}");

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // delete is blocked while point source references credential.
        Assert.Multiple(() =>
        {

            // Expected outcome: `deleted.StatusCode` has the required value.
            // Acceptance criteria: `deleted.StatusCode` must equal `HttpStatusCode.NoContent`, because this condition proves that
            // delete is blocked while point source references credential.
            Assert.That(deleted.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

            // Expected outcome: `deleted.Content.Headers.ContentLength` has the required value.
            // Acceptance criteria: `deleted.Content.Headers.ContentLength` must equal `0`, because this condition proves that
            // delete is blocked while point source references credential.
            Assert.That(deleted.Content.Headers.ContentLength, Is.EqualTo(0));
        });
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that create validates kinds and secrets.
    /// Description: Arranges the inputs for create validates kinds and secrets, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [TestCase("mqtt", "", "password", null, "username is required for MQTT credentials")]
    [TestCase("token", null, null, "", "a password or token is required")]
    [TestCase("other", null, null, "secret", "kind must be mqtt or token")]
    public async Task CreateValidatesKindsAndSecrets(
        string kind,
        string? username,
        string? password,
        string? token,
        string message)
    {
        await using var factory = new Api.FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        var input = new CredentialInput
        {
            Id = "invalid",
            Name = "Invalid",
            Kind = kind,
            Username = username,
            Password = password,
            Token = token
        };
        using var response = await client.PostAsJsonAsync(
            "/api/credentials",
            input,
            FlowControlJson.Options);
        var error = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // create validates kinds and secrets.
        Assert.Multiple(() =>
        {

            // Expected outcome: `response.StatusCode` has the required value.
            // Acceptance criteria: `response.StatusCode` must equal `HttpStatusCode.BadRequest`, because this condition proves that
            // create validates kinds and secrets.
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

            // Expected outcome: `error!["message"]` has the required value.
            // Acceptance criteria: `error!["message"]` must equal `message`, because this condition proves that
            // create validates kinds and secrets.
            Assert.That(error!["message"], Is.EqualTo(message));
        });
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that json decoder rejects unknown trailing and oversized bodies.
    /// Description: Arranges the inputs for json decoder rejects unknown trailing and oversized bodies, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public async Task JsonDecoderRejectsUnknownTrailingAndOversizedBodies()
    {
        await using var factory = new Api.FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        using var unknown = await PostRaw(
            client,
            """{"id":"token","name":"Token","kind":"token","token":"secret","extra":true}""");
        using var trailing = await PostRaw(
            client,
            """{"id":"token","name":"Token","kind":"token","token":"secret"} {}""");
        using var oversized = await PostRaw(
            client,
            "{\"id\":\"token\",\"name\":\""
                + new string('x', (64 << 10) + 1)
                + "\",\"kind\":\"token\",\"token\":\"secret\"}");

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // json decoder rejects unknown trailing and oversized bodies.
        Assert.Multiple(() =>
        {

            // Expected outcome: `unknown.StatusCode` has the required value.
            // Acceptance criteria: `unknown.StatusCode` must equal `HttpStatusCode.BadRequest`, because this condition proves that
            // json decoder rejects unknown trailing and oversized bodies.
            Assert.That(unknown.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

            // Expected outcome: `trailing.StatusCode` has the required value.
            // Acceptance criteria: `trailing.StatusCode` must equal `HttpStatusCode.BadRequest`, because this condition proves that
            // json decoder rejects unknown trailing and oversized bodies.
            Assert.That(trailing.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

            // Expected outcome: `oversized.StatusCode` has the required value.
            // Acceptance criteria: `oversized.StatusCode` must equal `HttpStatusCode.BadRequest`, because this condition proves that
            // json decoder rejects unknown trailing and oversized bodies.
            Assert.That(oversized.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        });
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that missing credential and invalid delete revision map correctly.
    /// Description: Arranges the inputs for missing credential and invalid delete revision map correctly, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public async Task MissingCredentialAndInvalidDeleteRevisionMapCorrectly()
    {
        await using var factory = new Api.FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        using var missing = await client.GetAsync("/api/credentials/missing");
        using var invalidDelete = await client.DeleteAsync(
            "/api/credentials/missing?revision=nope");

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // missing credential and invalid delete revision map correctly.
        Assert.Multiple(() =>
        {

            // Expected outcome: `missing.StatusCode` has the required value.
            // Acceptance criteria: `missing.StatusCode` must equal `HttpStatusCode.NotFound`, because this condition proves that
            // missing credential and invalid delete revision map correctly.
            Assert.That(missing.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

            // Expected outcome: `invalidDelete.StatusCode` has the required value.
            // Acceptance criteria: `invalidDelete.StatusCode` must equal `HttpStatusCode.BadRequest`, because this condition proves that
            // missing credential and invalid delete revision map correctly.
            Assert.That(invalidDelete.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        });
    }

    private static async Task<CredentialMetadata> Create(
        HttpClient client,
        CredentialInput input)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/credentials",
            input,
            FlowControlJson.Options);

        // Expected outcome: `response.StatusCode` has the required value.
        // Acceptance criteria: `response.StatusCode` must equal `HttpStatusCode.Created`, because this condition proves that
        // missing credential and invalid delete revision map correctly.
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var body = await response.Content.ReadAsStringAsync();

        // Expected outcome: `body.Contains(input.Password ?? input.Token!` rejects the prohibited condition.
        // Acceptance criteria: `body.Contains(input.Password ?? input.Token!` must be false, because this condition proves that
        // missing credential and invalid delete revision map correctly.
        Assert.That(
            body.Contains(input.Password ?? input.Token!, StringComparison.Ordinal),
            Is.False,
            "API response contains plaintext credential material");
        return System.Text.Json.JsonSerializer.Deserialize<CredentialMetadata>(
            body,
            FlowControlJson.Options)!;
    }

    private static Task<HttpResponseMessage> PostRaw(HttpClient client, string json) =>
        client.PostAsync(
            "/api/credentials",
            new StringContent(json, Encoding.UTF8, "application/json"));

    private static CredentialInput MqttCredential() => new()
    {
        Id = "plant-mqtt",
        Name = "Plant MQTT",
        Kind = "mqtt",
        Username = "reader",
        Password = "highly-secret"
    };

    private static CredentialInput TokenCredential() => new()
    {
        Id = "weather-token",
        Name = "Weather token",
        Kind = "token",
        Token = "weather-secret"
    };

    private static PointSource ValidHttpSource() => new()
    {
        Id = "weather",
        Name = "Weather",
        Enabled = true,
        Kind = "http_json",
        Connection = new PointSourceConnection
        {
            BaseUrl = "https://example.test",
            AllowedReadMethods = ["GET"],
            MaximumResponseBytes = 1024
        },
        CredentialRef = null,
        Tls = new TlsOptions { VerifyServerCertificate = true },
        Timeouts = new PointSourceTimeouts
        {
            ConnectMilliseconds = 100,
            RequestMilliseconds = 100
        }
    };
}