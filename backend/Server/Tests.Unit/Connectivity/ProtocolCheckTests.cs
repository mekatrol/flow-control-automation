using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Server.Services;
using Server.Services.Contracts;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Tests.Unit.Connectivity;

[TestFixture]
internal sealed class ProtocolCheckTests
{
    [Test]
    public async Task HttpCheckPinsAddressSendsBearerAndEnforcesResponseLimit()
    {
        await using var server = await LoopbackHttpServer.Start(
            "HTTP/1.1 200 OK\r\nContent-Length: 20\r\nConnection: close\r\n\r\n"
            + "12345678901234567890");
        using var factory = Factory(new FakeDns(IPAddress.Loopback));
        await using var scope = factory.Services.CreateAsyncScope();
        var check = scope.ServiceProvider.GetRequiredService<IHttpProtocolCheck>();
        var source = HttpSource(server.Url) with
        {
            Connection = HttpSource(server.Url).Connection with
            {
                MaximumResponseBytes = 10,
            },
        };

        var diagnostic = await check.CheckAsync(
            source,
            "bearer-value",
            [IPAddress.Loopback],
            CancellationToken.None);
        var request = await server.Request;
        Assert.Multiple(() =>
        {
            Assert.That(
                diagnostic,
                Is.EqualTo("HTTP response exceeded the configured size limit"));
            Assert.That(request, Does.Contain("Authorization: Bearer bearer-value"));
        });
    }

    [Test]
    public async Task RedirectDestinationIsRevalidatedBeforeConnection()
    {
        await using var server = await LoopbackHttpServer.Start(
            "HTTP/1.1 302 Found\r\n"
            + "Location: https://private.example.test/\r\n"
            + "Content-Length: 0\r\nConnection: close\r\n\r\n");
        using var factory = Factory(new FakeDns(IPAddress.Parse("192.168.1.20")));
        await using var scope = factory.Services.CreateAsyncScope();
        var check = scope.ServiceProvider.GetRequiredService<IHttpProtocolCheck>();
        var source = HttpSource(server.Url) with
        {
            Connection = HttpSource(server.Url).Connection with
            {
                FollowRedirects = true,
            },
        };

        var diagnostic = await check.CheckAsync(
            source,
            string.Empty,
            [IPAddress.Loopback],
            CancellationToken.None);
        Assert.That(diagnostic, Is.EqualTo("redirect destination is forbidden"));
    }

    [Test]
    public async Task MqttCheckAuthenticatesSubscribesAndDisconnects()
    {
        var replies = new byte[]
        {
            0x20, 0x02, 0x00, 0x00,
            0x90, 0x03, 0x00, 0x01, 0x01,
        };
        await using var stream = new ScriptedStream(replies);
        using var factory = new Api.FlowControlApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var check = scope.ServiceProvider.GetRequiredService<IMqttProtocolCheck>();
        var source = MqttSource();

        var diagnostic = await check.CheckAsync(
            stream,
            source,
            """{"username":"reader","password":"mqtt-secret"}""",
            CancellationToken.None);
        var written = stream.Written.ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(diagnostic, Is.Null);
            Assert.That(
                Encoding.UTF8.GetString(written).Contains(
                    "plant/temperature",
                    StringComparison.Ordinal),
                Is.True);
            Assert.That(written[^2..], Is.EqualTo(new byte[] { 0xe0, 0x00 }));
        });
    }

    [Test]
    public async Task MqttCheckRejectsUnstructuredCredential()
    {
        await using var stream = new ScriptedStream([]);
        using var factory = new Api.FlowControlApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var check = scope.ServiceProvider.GetRequiredService<IMqttProtocolCheck>();
        var diagnostic = await check.CheckAsync(
            stream,
            MqttSource(),
            "reader:secret",
            CancellationToken.None);
        Assert.That(
            diagnostic,
            Is.EqualTo("MQTT credential must be JSON with username and password"));
    }

    [TestCase(0x01, "MQTT connection rejected: unacceptable protocol version")]
    [TestCase(0x02, "MQTT connection rejected: client identifier rejected")]
    [TestCase(0x03, "MQTT connection rejected: broker unavailable")]
    [TestCase(0x04, "MQTT connection rejected: bad username or password")]
    [TestCase(0x05, "MQTT connection rejected: not authorized")]
    [TestCase(0x80, "MQTT connection rejected: unknown CONNACK code 0x80")]
    public async Task MqttCheckReportsConnackRejectionReason(
        byte returnCode,
        string expectedDiagnostic)
    {
        await using var stream = new ScriptedStream([0x20, 0x02, 0x00, returnCode]);
        using var factory = new Api.FlowControlApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var check = scope.ServiceProvider.GetRequiredService<IMqttProtocolCheck>();

        var diagnostic = await check.CheckAsync(
            stream,
            MqttSource(),
            """{"username":"reader","password":"mqtt-secret"}""",
            CancellationToken.None);

        Assert.That(diagnostic, Is.EqualTo(expectedDiagnostic));
    }

    private static Api.FlowControlApplicationFactory Factory(IDnsLookup dns) =>
        new(services =>
        {
            services.RemoveAll<IDnsLookup>();
            services.AddSingleton(dns);
        });

    private static PointSource HttpSource(Uri uri) => new()
    {
        Id = "http-check",
        Name = "HTTP check",
        Kind = "http_json",
        Connection = new PointSourceConnection
        {
            BaseUrl = uri.ToString(),
            AllowedReadMethods = ["GET"],
            FollowRedirects = false,
            MaximumResponseBytes = 1024,
        },
        Tls = new TlsOptions { VerifyServerCertificate = true },
        Timeouts = new PointSourceTimeouts
        {
            ConnectMilliseconds = 1000,
            RequestMilliseconds = 1000,
        },
    };

    private static PointSource MqttSource() => new()
    {
        Id = "plant-mqtt",
        Name = "Plant MQTT",
        Kind = "mqtt",
        Connection = new PointSourceConnection
        {
            BrokerUrl = "mqtt://example.test:1883",
            ClientIdPrefix = "test",
            TestTopic = "plant/temperature",
            Qos = 1,
        },
        Tls = new TlsOptions(),
        Timeouts = new PointSourceTimeouts
        {
            ConnectMilliseconds = 100,
            RequestMilliseconds = 100,
        },
    };

    private sealed class FakeDns(IPAddress address) : IDnsLookup
    {
        public Task<IReadOnlyList<IPAddress>> LookupAsync(
            string host,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<IPAddress>>([address]);
    }

    private sealed class ScriptedStream(byte[] replies) : Stream
    {
        private readonly MemoryStream _replies = new(replies);

        public MemoryStream Written { get; } = new();

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            _replies.Read(buffer, offset, count);

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            _replies.ReadAsync(buffer, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            Written.Write(buffer, offset, count);

        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            Written.WriteAsync(buffer, cancellationToken);
    }

    private sealed class LoopbackHttpServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly Task _serve;

        private LoopbackHttpServer(TcpListener listener, string response)
        {
            _listener = listener;
            Url = new Uri(
                $"http://localhost:{((IPEndPoint)listener.LocalEndpoint).Port}/");
            var requestCompletion =
                new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            Request = requestCompletion.Task;
            _serve = Serve(response, requestCompletion);
        }

        public Uri Url { get; }

        public Task<string> Request { get; }

        public static Task<LoopbackHttpServer> Start(string response)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return Task.FromResult(new LoopbackHttpServer(listener, response));
        }

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            try
            {
                await _serve;
            }
            catch (Exception exception) when (
                exception is SocketException or ObjectDisposedException)
            {
            }
        }

        private async Task Serve(
            string response,
            TaskCompletionSource<string> requestCompletion)
        {
            using var client = await _listener.AcceptTcpClientAsync();
            await using var stream = client.GetStream();
            var buffer = new byte[8192];
            var length = 0;
            while (length < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(length));
                if (read == 0)
                {
                    break;
                }

                length += read;
                if (Encoding.ASCII.GetString(buffer, 0, length).Contains(
                    "\r\n\r\n",
                    StringComparison.Ordinal))
                {
                    break;
                }
            }

            requestCompletion.TrySetResult(Encoding.ASCII.GetString(buffer, 0, length));
            await stream.WriteAsync(Encoding.ASCII.GetBytes(response));
        }
    }
}