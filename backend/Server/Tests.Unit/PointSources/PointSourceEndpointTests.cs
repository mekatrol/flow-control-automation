using Server.Services.Contracts;
using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace Tests.Unit.PointSources;

[TestFixture]
internal sealed class PointSourceEndpointTests
{
    [Test]
    public async Task CrudUsesYamlEtagsAndRevisionConflicts()
    {
        using var factory = new Api.FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        var source = ValidHttpSource();

        using var create = await SendYaml(client, HttpMethod.Post, "/api/point-sources", source);
        var created = await ReadSource(create);
        Assert.That(create.Headers.TryGetValues("ETag", out var createEtags), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(create.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(create.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/yaml"));
            Assert.That(createEtags, Is.EqualTo(new[] { "1" }));
            Assert.That(created.Revision, Is.Zero);
            Assert.That(created.CreatedAt, Is.Null);
            Assert.That(created.UpdatedAt, Is.Null);
        });

        using var get = await client.GetAsync("/api/point-sources/weather");
        Assert.That(get.Headers.TryGetValues("ETag", out var getEtags), Is.True);
        Assert.That(getEtags, Is.EqualTo(new[] { "1" }));

        var updatedInput = source with { Name = "Updated weather" };
        using var update = await SendYaml(
            client,
            HttpMethod.Put,
            "/api/point-sources/weather",
            updatedInput,
            revision: 1);
        var updated = await ReadSource(update);
        Assert.That(update.Headers.TryGetValues("ETag", out var updateEtags), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(update.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(updateEtags, Is.EqualTo(new[] { "2" }));
            Assert.That(updated.Name, Is.EqualTo("Updated weather"));
        });

        using var staleUpdate = await SendYaml(
            client,
            HttpMethod.Put,
            "/api/point-sources/weather",
            source,
            revision: 1);
        Assert.That(staleUpdate.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));

        using var staleDelete = await client.DeleteAsync(
            "/api/point-sources/weather?revision=1");
        Assert.That(staleDelete.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));

        using var delete = await client.DeleteAsync("/api/point-sources/weather?revision=2");
        Assert.Multiple(() =>
        {
            Assert.That(delete.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(delete.Content.Headers.ContentLength, Is.EqualTo(0));
        });
        using var missing = await client.GetAsync("/api/point-sources/weather");
        Assert.That(missing.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task ListFiltersSortsPaginatesAndPersistsAcrossScopes()
    {
        using var factory = new Api.FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        for (var index = 1; index <= 12; index++)
        {
            var source = ValidHttpSource() with
            {
                Id = $"weather-{index}",
                Name = $"Weather {index:00}",
            };
            using var response = await SendYaml(
                client,
                HttpMethod.Post,
                "/api/point-sources",
                source);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        }

        using var secondClient = factory.CreateClient();
        var page = await secondClient.GetFromJsonAsync<PaginatedResult<PointSource>>(
            "/api/point-sources?page=2&pageSize=10&filter=WEATHER&sort=descending",
            FlowControlJson.Options);
        Assert.Multiple(() =>
        {
            Assert.That(page, Is.Not.Null);
            Assert.That(page!.TotalItems, Is.EqualTo(12));
            Assert.That(page.PageCount, Is.EqualTo(2));
            Assert.That(page.Page, Is.EqualTo(2));
            Assert.That(page.Items, Has.Count.EqualTo(2));
            Assert.That(page.Items[0].Name, Is.EqualTo("Weather 02"));
            Assert.That(page.Items[1].Name, Is.EqualTo("Weather 01"));
            Assert.That(page.Items[0].Revision, Is.EqualTo(1));
            Assert.That(page.Items[0].CreatedAt, Is.Not.Null);
        });
    }

    [TestCase("/api/point-sources?page=0")]
    [TestCase("/api/point-sources?page=nope")]
    [TestCase("/api/point-sources?pageSize=100")]
    [TestCase("/api/point-sources?sort=sideways")]
    public async Task ListRejectsInvalidQueries(string path)
    {
        using var factory = new Api.FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(path);
        var error = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(error!["message"], Is.EqualTo("invalid pagination or sort query"));
        });
    }

    [Test]
    public async Task ValidationAndDuplicateNamesRollBack()
    {
        using var factory = new Api.FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        var source = ValidHttpSource();
        using var created = await SendYaml(client, HttpMethod.Post, "/api/point-sources", source);
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        using var duplicateName = await SendYaml(
            client,
            HttpMethod.Post,
            "/api/point-sources",
            source with { Id = "forecast" });
        Assert.That(duplicateName.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));

        using var unsafeMethod = await SendYaml(
            client,
            HttpMethod.Post,
            "/api/point-sources",
            source with
            {
                Id = "unsafe",
                Name = "Unsafe",
                Connection = source.Connection with { AllowedReadMethods = ["POST"] },
            });
        Assert.That(unsafeMethod.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        using var loaded = await client.GetAsync("/api/point-sources/weather");
        Assert.That(loaded.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task UpdateRequiresIfMatchAndMatchingPath()
    {
        using var factory = new Api.FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        var source = ValidHttpSource();
        using var created = await SendYaml(client, HttpMethod.Post, "/api/point-sources", source);
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        using var missingHeader = await SendYaml(
            client,
            HttpMethod.Put,
            "/api/point-sources/weather",
            source);
        using var mismatch = await SendYaml(
            client,
            HttpMethod.Put,
            "/api/point-sources/weather",
            source with { Id = "different" },
            revision: 1);
        Assert.Multiple(() =>
        {
            Assert.That(missingHeader.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(mismatch.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        });
    }

    [Test]
    public async Task StrictYamlAndRequestLimitAreEnforced()
    {
        using var factory = new Api.FlowControlApplicationFactory();
        using var client = factory.CreateClient();
        const string duplicate = """
            schemaVersion: 1
            sources:
              - id: weather
                name: Weather
                name: Duplicate
                enabled: true
                kind: http_json
                connection:
                  baseUrl: https://example.test
                  allowedReadMethods: [GET]
                  maximumResponseBytes: 1024
                tls: {verifyServerCertificate: true}
                timeouts: {connectMilliseconds: 100, requestMilliseconds: 100}
            """;
        using var duplicateResponse = await SendRawYaml(client, duplicate);
        Assert.That(duplicateResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var oversized = new string(' ', ConfigurationYaml.MaximumBytes + 1);
        using var oversizedResponse = await SendRawYaml(client, oversized);
        var error =
            await oversizedResponse.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Multiple(() =>
        {
            Assert.That(oversizedResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(error!["message"], Is.EqualTo("unable to read YAML request"));
        });
    }

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
            FollowRedirects = false,
            MaximumResponseBytes = 1024,
        },
        Tls = new TlsOptions { VerifyServerCertificate = true },
        Timeouts = new PointSourceTimeouts
        {
            ConnectMilliseconds = 100,
            RequestMilliseconds = 100,
        },
    };

    private static async Task<HttpResponseMessage> SendYaml(
        HttpClient client,
        HttpMethod method,
        string path,
        PointSource source,
        int? revision = null)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = new StringContent(
                PointSourceYaml.Render(source),
                Encoding.UTF8,
                "application/yaml"),
        };
        if (revision is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", revision.Value.ToString());
        }

        return await client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> SendRawYaml(HttpClient client, string yaml)
    {
        var content = new StringContent(yaml, Encoding.UTF8, "application/yaml");
        return client.PostAsync("/api/point-sources", content);
    }

    private static async Task<PointSource> ReadSource(HttpResponseMessage response) =>
        PointSourceYaml.Parse(
            Encoding.UTF8.GetBytes(await response.Content.ReadAsStringAsync()));
}