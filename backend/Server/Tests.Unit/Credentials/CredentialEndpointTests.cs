using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
    [Test]
    public async Task StoreEncryptsSecretsAndResolverSurvivesScopeRestart()
    {
        using var factory = new Api.FlowControlApplicationFactory();
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
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(metadata!.Username, Is.EqualTo("reader"));
            Assert.That(metadata.Revision, Is.EqualTo(1));
            Assert.That(
                body.Contains("highly-secret", StringComparison.Ordinal),
                Is.False,
                "API response contains plaintext credential material");
            Assert.That(body, Does.Not.Contain("\"password\""));
            Assert.That(body, Does.Not.Contain("\"token\""));
        });

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IFlowControlDbContext>();
            var row = await context.Credentials.AsNoTracking().SingleAsync();
            Assert.Multiple(() =>
            {
                Assert.That(
                    row.Json.Contains("highly-secret", StringComparison.Ordinal),
                    Is.False,
                    "database row contains plaintext credential material");
                Assert.That(row.Json, Does.Contain("\"secret\""));
            });

            var resolver = scope.ServiceProvider.GetRequiredService<ICredentialResolver>();
            var resolved = await resolver.ResolveAsync(
                "secret://plant-mqtt",
                CancellationToken.None);
            Assert.Multiple(() =>
            {
                Assert.That(resolved, Does.Contain("\"username\":\"reader\""));
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
            Assert.That(
                resolved.Contains("highly-secret", StringComparison.Ordinal),
                Is.True,
                "credential did not resolve after creating a new scope");
        }
    }

    [Test]
    public async Task CrudListsMetadataAndPreservesSecretWhenUpdateOmitsIt()
    {
        using var factory = new Api.FlowControlApplicationFactory();
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

        Assert.Multiple(() =>
        {
            Assert.That(get.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(loaded!.Name, Is.EqualTo("Weather token"));
            Assert.That(getBody, Does.Not.Contain("weather-secret"));
            Assert.That(update.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(updated!.Revision, Is.EqualTo(2));
            Assert.That(updated.Name, Is.EqualTo("Updated weather token"));
            Assert.That(list!.Items, Has.Count.EqualTo(1));
            Assert.That(list.Items[0].Revision, Is.EqualTo(2));
        });

        await using var scope = factory.Services.CreateAsyncScope();
        var resolver = scope.ServiceProvider.GetRequiredService<ICredentialResolver>();
        var resolved = await resolver.ResolveAsync(
            "secret://weather-token",
            CancellationToken.None);
        Assert.That(
            resolved == "weather-secret",
            Is.True,
            "updating metadata changed the stored secret");
    }

    [Test]
    public async Task StaleRevisionMismatchedIdAndDuplicateNameConflict()
    {
        using var factory = new Api.FlowControlApplicationFactory();
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
        Assert.Multiple(() =>
        {
            Assert.That(stale.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(mismatch.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(duplicateName.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(staleDelete.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        });
    }

    [Test]
    public async Task DeleteIsBlockedWhilePointSourceReferencesCredential()
    {
        using var factory = new Api.FlowControlApplicationFactory();
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
        Assert.That(sourceResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        using var blocked = await client.DeleteAsync(
            $"/api/credentials/weather-token?revision={credential.Revision}");
        var error = await blocked.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Multiple(() =>
        {
            Assert.That(blocked.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(error!["message"], Does.Contain("\"weather\""));
        });

        using var deleteSource = await client.DeleteAsync(
            "/api/point-sources/weather?revision=1");
        Assert.That(deleteSource.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        using var deleted = await client.DeleteAsync(
            $"/api/credentials/weather-token?revision={credential.Revision}");
        Assert.Multiple(() =>
        {
            Assert.That(deleted.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(deleted.Content.Headers.ContentLength, Is.EqualTo(0));
        });
    }

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
        using var factory = new Api.FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        var input = new CredentialInput
        {
            Id = "invalid",
            Name = "Invalid",
            Kind = kind,
            Username = username,
            Password = password,
            Token = token,
        };
        using var response = await client.PostAsJsonAsync(
            "/api/credentials",
            input,
            FlowControlJson.Options);
        var error = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(error!["message"], Is.EqualTo(message));
        });
    }

    [Test]
    public async Task JsonDecoderRejectsUnknownTrailingAndOversizedBodies()
    {
        using var factory = new Api.FlowControlApplicationFactory();
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
        Assert.Multiple(() =>
        {
            Assert.That(unknown.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(trailing.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(oversized.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        });
    }

    [Test]
    public async Task MissingCredentialAndInvalidDeleteRevisionMapCorrectly()
    {
        using var factory = new Api.FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        using var missing = await client.GetAsync("/api/credentials/missing");
        using var invalidDelete = await client.DeleteAsync(
            "/api/credentials/missing?revision=nope");
        Assert.Multiple(() =>
        {
            Assert.That(missing.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
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
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var body = await response.Content.ReadAsStringAsync();
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
        Password = "highly-secret",
    };

    private static CredentialInput TokenCredential() => new()
    {
        Id = "weather-token",
        Name = "Weather token",
        Kind = "token",
        Token = "weather-secret",
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
            MaximumResponseBytes = 1024,
        },
        CredentialRef = null,
        Tls = new TlsOptions { VerifyServerCertificate = true },
        Timeouts = new PointSourceTimeouts
        {
            ConnectMilliseconds = 100,
            RequestMilliseconds = 100,
        },
    };
}