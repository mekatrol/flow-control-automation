using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Server.Services;
using Server.Services.Contracts;
using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace Tests.Unit.Connectivity;

[TestFixture]
internal sealed class ConnectivityEndpointTests
{
    [Test]
    public async Task UnsavedTestRejectsPrivateAndLoopbackDestinations()
    {
        var dns = new FakeDns(IPAddress.Parse("192.168.1.20"));
        await using var factory = Factory(dns: dns);
        using var client = factory.CreateClient();

        using var privateResponse = await TestUnsaved(client, ValidHttpSource());
        var privateResult = await privateResponse.Content.ReadFromJsonAsync<ConnectivityResult>(
            FlowControlJson.Options);
        dns.Addresses = [IPAddress.Loopback];
        using var loopbackResponse = await TestUnsaved(
            client,
            ValidHttpSource() with
            {
                Connection = ValidHttpSource().Connection with
                {
                    AllowPrivateNetwork = true,
                }
            });
        var loopbackResult =
            await loopbackResponse.Content.ReadFromJsonAsync<ConnectivityResult>(
                FlowControlJson.Options);

        Assert.Multiple(() =>
        {
            Assert.That(privateResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(privateResult!.Status, Is.EqualTo("failed"));
            Assert.That(privateResult.Stages[^1].Name, Is.EqualTo("dns"));
            Assert.That(privateResult.Stages[^1].Diagnostic, Does.Contain("forbidden"));
            Assert.That(loopbackResult!.Stages[^1].Diagnostic, Does.Contain("forbidden"));
        });
    }

    [Test]
    public async Task PrivateNetworkOptInPassesWithInjectedProtocolChecks()
    {
        var http = new FakeHttpCheck();
        await using var factory = Factory(dns: new FakeDns(IPAddress.Parse("192.168.1.20")), http: http);
        using var client = factory.CreateClient();
        var source = ValidHttpSource() with
        {
            Connection = ValidHttpSource().Connection with
            {
                AllowPrivateNetwork = true,
            }
        };

        using var response = await TestUnsaved(client, source);
        var result = await response.Content.ReadFromJsonAsync<ConnectivityResult>(
            FlowControlJson.Options);
        Assert.Multiple(() =>
        {
            Assert.That(result!.Status, Is.EqualTo("passed"));
            Assert.That(
                result.Stages.Select(stage => stage.Name),
                Is.EqualTo(new[] { "dns", "tcp", "tls", "authentication", "protocol" }));
            Assert.That(http.Calls, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task SavedRouteUsesResolvedCredentialWithoutReturningIt()
    {
        const string credential = "sensitive-connectivity-token";
        var http = new FakeHttpCheck();
        await using var factory = Factory(
            dns: new FakeDns(IPAddress.Parse("8.8.8.8")),
            http: http,
            resolver: new FakeResolver(credential));
        using var client = factory.CreateClient();
        var source = ValidHttpSource() with { CredentialRef = "env:TEST_TOKEN" };
        using var create = await client.PostAsync(
            "/api/point-sources",
            Yaml(source));
        Assert.That(create.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        using var response = await client.PostAsync(
            "/api/point-sources/weather/test",
            content: null);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(http.CredentialReceived == credential, Is.True);
            Assert.That(
                body.Contains(credential, StringComparison.Ordinal),
                Is.False,
                "connectivity response contains credential material");
        });
    }

    [Test]
    public async Task EleventhTestForClientIsRateLimited()
    {
        await using var factory = Factory(
            dns: new FakeDns(IPAddress.Parse("8.8.8.8")));
        using var client = factory.CreateClient();
        ConnectivityResult? result = null;
        for (var index = 0; index < 11; index++)
        {
            using var response = await TestUnsaved(client, ValidHttpSource());
            result = await response.Content.ReadFromJsonAsync<ConnectivityResult>(
                FlowControlJson.Options);
        }

        Assert.Multiple(() =>
        {
            Assert.That(result!.Status, Is.EqualTo("failed"));
            Assert.That(result.Stages[0].Name, Is.EqualTo("policy"));
            Assert.That(result.Stages[0].Diagnostic, Does.Contain("rate limit"));
        });
    }

    [Test]
    public async Task CancellationProducesRedactedDiagnostic()
    {
        await using var factory = Factory(dns: new FakeDns(cancel: true));
        using var client = factory.CreateClient();
        using var response = await TestUnsaved(client, ValidHttpSource());
        var result = await response.Content.ReadFromJsonAsync<ConnectivityResult>(
            FlowControlJson.Options);
        Assert.Multiple(() =>
        {
            Assert.That(result!.Status, Is.EqualTo("failed"));
            Assert.That(result.Stages[^1].Name, Is.EqualTo("dns"));
            Assert.That(
                result.Stages[^1].Diagnostic,
                Is.EqualTo("connection test cancelled"));
        });
    }

    [Test]
    public async Task InvalidUnsavedSourceReturnsValidationStage()
    {
        await using var factory = Factory();
        using var client = factory.CreateClient();
        var source = ValidHttpSource() with
        {
            Connection = ValidHttpSource().Connection with
            {
                AllowedReadMethods = ["POST"],
            },
        };
        using var response = await TestUnsaved(client, source);
        var result = await response.Content.ReadFromJsonAsync<ConnectivityResult>(
            FlowControlJson.Options);
        Assert.Multiple(() =>
        {
            Assert.That(result!.Status, Is.EqualTo("failed"));
            Assert.That(result.Stages[0].Name, Is.EqualTo("validation"));
            Assert.That(result.Stages[0].Diagnostic, Does.Contain("GET and HEAD"));
        });
    }

    [Test]
    public async Task MissingSavedSourceReturnsNotFound()
    {
        await using var factory = Factory();
        using var client = factory.CreateClient();
        using var response = await client.PostAsync(
            "/api/point-sources/missing/test",
            content: null);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    private static Api.FlowControlApplicationFactory Factory(
        FakeDns? dns = null,
        FakeHttpCheck? http = null,
        ICredentialResolver? resolver = null) =>
        new(services =>
        {
            Replace<IDnsLookup>(services, dns ?? new FakeDns(IPAddress.Parse("8.8.8.8")));
            Replace<ITcpConnectionFactory>(services, new FakeTcp());
            Replace<ITlsHandshake>(services, new FakeTls());
            Replace<IHttpProtocolCheck>(services, http ?? new FakeHttpCheck());
            Replace<IMqttProtocolCheck>(services, new FakeMqttCheck());
            if (resolver is not null)
            {
                Replace(services, resolver);
            }
        });

    private static void Replace<TService>(
        IServiceCollection services,
        TService replacement)
        where TService : class
    {
        services.RemoveAll<TService>();
        services.AddSingleton(replacement);
    }

    private static Task<HttpResponseMessage> TestUnsaved(
        HttpClient client,
        PointSource source) =>
        client.PostAsync("/api/point-sources/test", Yaml(source));

    private static StringContent Yaml(PointSource source) =>
        new(
            PointSourceYaml.Render(source),
            Encoding.UTF8,
            "application/yaml");

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
        Tls = new TlsOptions { VerifyServerCertificate = true },
        Timeouts = new PointSourceTimeouts
        {
            ConnectMilliseconds = 100,
            RequestMilliseconds = 100
        }
    };

    private sealed class FakeDns(
        IPAddress? address = null,
        bool cancel = false) : IDnsLookup
    {
        public IReadOnlyList<IPAddress> Addresses { get; set; } =
            address is null ? [] : [address];

        public Task<IReadOnlyList<IPAddress>> LookupAsync(
            string host,
            CancellationToken cancellationToken) =>
            cancel
                ? Task.FromCanceled<IReadOnlyList<IPAddress>>(
                    new CancellationToken(canceled: true))
                : Task.FromResult(Addresses);
    }

    private sealed class FakeTcp : ITcpConnectionFactory
    {
        public Task<Stream> ConnectAsync(
            string host,
            int port,
            TimeSpan timeout,
            CancellationToken cancellationToken) =>
            Task.FromResult<Stream>(new MemoryStream());
    }

    private sealed class FakeTls : ITlsHandshake
    {
        public Task<Stream> AuthenticateAsync(
            Stream stream,
            string host,
            TimeSpan timeout,
            CancellationToken cancellationToken) =>
            Task.FromResult(stream);
    }

    private sealed class FakeHttpCheck : IHttpProtocolCheck
    {
        public int Calls { get; private set; }

        public string? CredentialReceived { get; private set; }

        public Task<string?> CheckAsync(
            PointSource source,
            string credential,
            IReadOnlyList<IPAddress> pinnedAddresses,
            CancellationToken cancellationToken)
        {
            Calls++;
            CredentialReceived = credential;
            return Task.FromResult<string?>(null);
        }
    }

    private sealed class FakeMqttCheck : IMqttProtocolCheck
    {
        public Task<string?> CheckAsync(
            Stream stream,
            PointSource source,
            string credential,
            CancellationToken cancellationToken) =>
            Task.FromResult<string?>(null);
    }

    private sealed class FakeResolver(string value) : ICredentialResolver
    {
        public Task<string> ResolveAsync(
            string reference,
            CancellationToken cancellationToken) =>
            Task.FromResult(value);
    }
}