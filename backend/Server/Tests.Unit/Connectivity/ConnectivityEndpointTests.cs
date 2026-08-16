using Server.Services;
using Server.Services.Contracts;
using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace Tests.Unit.Connectivity;

[TestFixture]
internal sealed class ConnectivityEndpointTests
{
    /// <summary>
    /// Purpose: Protects the behavioral contract that unsaved test rejects private and loopback destinations.
    /// Description: Arranges the inputs for unsaved test rejects private and loopback destinations, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
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

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // unsaved test rejects private and loopback destinations.
        Assert.Multiple(() =>
        {
            // Expected outcome: `privateResponse.StatusCode` has the required value.
            // Acceptance criteria: `privateResponse.StatusCode` must equal `HttpStatusCode.OK`, because this condition proves that
            // unsaved test rejects private and loopback destinations.
            Assert.That(privateResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            // Expected outcome: `privateResult!.Status` has the required value.
            // Acceptance criteria: `privateResult!.Status` must equal `"failed"`, because this condition proves that
            // unsaved test rejects private and loopback destinations.
            Assert.That(privateResult!.Status, Is.EqualTo("failed"));

            // Expected outcome: `privateResult.Stages[^1].Name` has the required value.
            // Acceptance criteria: `privateResult.Stages[^1].Name` must equal `"dns"`, because this condition proves that
            // unsaved test rejects private and loopback destinations.
            Assert.That(privateResult.Stages[^1].Name, Is.EqualTo("dns"));

            // Expected outcome: `privateResult.Stages[^1].Diagnostic` includes the required content.
            // Acceptance criteria: `privateResult.Stages[^1].Diagnostic` must contain `"forbidden"`, because this condition proves that
            // unsaved test rejects private and loopback destinations.
            Assert.That(privateResult.Stages[^1].Diagnostic, Does.Contain("forbidden"));

            // Expected outcome: `loopbackResult!.Stages[^1].Diagnostic` includes the required content.
            // Acceptance criteria: `loopbackResult!.Stages[^1].Diagnostic` must contain `"forbidden"`, because this condition proves that
            // unsaved test rejects private and loopback destinations.
            Assert.That(loopbackResult!.Stages[^1].Diagnostic, Does.Contain("forbidden"));
        });
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that private network opt in passes with injected protocol checks.
    /// Description: Arranges the inputs for private network opt in passes with injected protocol checks, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
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

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // private network opt in passes with injected protocol checks.
        Assert.Multiple(() =>
        {
            // Expected outcome: `result!.Status` has the required value.
            // Acceptance criteria: `result!.Status` must equal `"passed"`, because this condition proves that
            // private network opt in passes with injected protocol checks.
            Assert.That(result!.Status, Is.EqualTo("passed"));

            // Expected outcome: `result.Stages.Select(stage => stage.Name` has the required value.
            // Acceptance criteria: `result.Stages.Select(stage => stage.Name` must equal `new[] { "dns", "tcp", "tls", "authentication", "protocol" }`, because this condition proves that
            // private network opt in passes with injected protocol checks.
            Assert.That(
                result.Stages.Select(stage => stage.Name),
                Is.EqualTo(new[] { "dns", "tcp", "tls", "authentication", "protocol" }));

            // Expected outcome: `http.Calls` has the required value.
            // Acceptance criteria: `http.Calls` must equal `1`, because this condition proves that
            // private network opt in passes with injected protocol checks.
            Assert.That(http.Calls, Is.EqualTo(1));
        });
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that saved route uses resolved credential without returning it.
    /// Description: Arranges the inputs for saved route uses resolved credential without returning it, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
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

        // Expected outcome: `create.StatusCode` has the required value.
        // Acceptance criteria: `create.StatusCode` must equal `HttpStatusCode.Created`, because this condition proves that
        // saved route uses resolved credential without returning it.
        Assert.That(create.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        using var response = await client.PostAsync(
            "/api/point-sources/weather/test",
            content: null);
        var body = await response.Content.ReadAsStringAsync();

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // saved route uses resolved credential without returning it.
        Assert.Multiple(() =>
        {
            // Expected outcome: `response.StatusCode` has the required value.
            // Acceptance criteria: `response.StatusCode` must equal `HttpStatusCode.OK`, because this condition proves that
            // saved route uses resolved credential without returning it.
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            // Expected outcome: `http.CredentialReceived == credential` confirms the required condition.
            // Acceptance criteria: `http.CredentialReceived == credential` must be true, because this condition proves that
            // saved route uses resolved credential without returning it.
            Assert.That(http.CredentialReceived == credential, Is.True);

            // Expected outcome: `body.Contains(credential` rejects the prohibited condition.
            // Acceptance criteria: `body.Contains(credential` must be false, because this condition proves that
            // saved route uses resolved credential without returning it.
            Assert.That(
                body.Contains(credential, StringComparison.Ordinal),
                Is.False,
                "connectivity response contains credential material");
        });
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that eleventh test for client is rate limited.
    /// Description: Arranges the inputs for eleventh test for client is rate limited, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
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

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // eleventh test for client is rate limited.
        Assert.Multiple(() =>
        {
            // Expected outcome: `result!.Status` has the required value.
            // Acceptance criteria: `result!.Status` must equal `"failed"`, because this condition proves that
            // eleventh test for client is rate limited.
            Assert.That(result!.Status, Is.EqualTo("failed"));

            // Expected outcome: `result.Stages[0].Name` has the required value.
            // Acceptance criteria: `result.Stages[0].Name` must equal `"policy"`, because this condition proves that
            // eleventh test for client is rate limited.
            Assert.That(result.Stages[0].Name, Is.EqualTo("policy"));

            // Expected outcome: `result.Stages[0].Diagnostic` includes the required content.
            // Acceptance criteria: `result.Stages[0].Diagnostic` must contain `"rate limit"`, because this condition proves that
            // eleventh test for client is rate limited.
            Assert.That(result.Stages[0].Diagnostic, Does.Contain("rate limit"));
        });
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that cancellation produces redacted diagnostic.
    /// Description: Arranges the inputs for cancellation produces redacted diagnostic, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public async Task CancellationProducesRedactedDiagnostic()
    {
        await using var factory = Factory(dns: new FakeDns(cancel: true));
        using var client = factory.CreateClient();
        using var response = await TestUnsaved(client, ValidHttpSource());
        var result = await response.Content.ReadFromJsonAsync<ConnectivityResult>(
            FlowControlJson.Options);

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // cancellation produces redacted diagnostic.
        Assert.Multiple(() =>
        {
            // Expected outcome: `result!.Status` has the required value.
            // Acceptance criteria: `result!.Status` must equal `"failed"`, because this condition proves that
            // cancellation produces redacted diagnostic.
            Assert.That(result!.Status, Is.EqualTo("failed"));

            // Expected outcome: `result.Stages[^1].Name` has the required value.
            // Acceptance criteria: `result.Stages[^1].Name` must equal `"dns"`, because this condition proves that
            // cancellation produces redacted diagnostic.
            Assert.That(result.Stages[^1].Name, Is.EqualTo("dns"));

            // Expected outcome: `result.Stages[^1].Diagnostic` has the required value.
            // Acceptance criteria: `result.Stages[^1].Diagnostic` must equal `"connection test cancelled"`, because this condition proves that
            // cancellation produces redacted diagnostic.
            Assert.That(
                result.Stages[^1].Diagnostic,
                Is.EqualTo("connection test cancelled"));
        });
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that invalid unsaved source returns validation stage.
    /// Description: Arranges the inputs for invalid unsaved source returns validation stage, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
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

        // Expected outcome: All related outcomes satisfy their contracts.
        // Acceptance criteria: every assertion in the group must pass, because this condition proves that
        // invalid unsaved source returns validation stage.
        Assert.Multiple(() =>
        {
            // Expected outcome: `result!.Status` has the required value.
            // Acceptance criteria: `result!.Status` must equal `"failed"`, because this condition proves that
            // invalid unsaved source returns validation stage.
            Assert.That(result!.Status, Is.EqualTo("failed"));

            // Expected outcome: `result.Stages[0].Name` has the required value.
            // Acceptance criteria: `result.Stages[0].Name` must equal `"validation"`, because this condition proves that
            // invalid unsaved source returns validation stage.
            Assert.That(result.Stages[0].Name, Is.EqualTo("validation"));

            // Expected outcome: `result.Stages[0].Diagnostic` includes the required content.
            // Acceptance criteria: `result.Stages[0].Diagnostic` must contain `"GET and HEAD"`, because this condition proves that
            // invalid unsaved source returns validation stage.
            Assert.That(result.Stages[0].Diagnostic, Does.Contain("GET and HEAD"));
        });
    }

    /// <summary>
    /// Purpose: Protects the behavioral contract that missing saved source returns not found.
    /// Description: Arranges the inputs for missing saved source returns not found, exercises the relevant operation,
    /// and verifies the observable results required by that scenario.
    /// </summary>
    [Test]
    public async Task MissingSavedSourceReturnsNotFound()
    {
        await using var factory = Factory();
        using var client = factory.CreateClient();
        using var response = await client.PostAsync(
            "/api/point-sources/missing/test",
            content: null);

        // Expected outcome: `response.StatusCode` has the required value.
        // Acceptance criteria: `response.StatusCode` must equal `HttpStatusCode.NotFound`, because this condition proves that
        // missing saved source returns not found.
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