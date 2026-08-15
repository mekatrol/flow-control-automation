using Server.Data;
using Server.Data.Extensions;
using Server.Services;
using Server.Services.Contracts;

namespace Tests.Unit.Api;

internal sealed class FlowControlApplicationFactory(
    Action<IServiceCollection>? configureServices = null) : WebApplicationFactory<Server.Api.Program>
{
    private const string TestCredentialEncryptionKey =
        "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=";

    private readonly string _temporaryDirectory = Path.Combine(
        Path.GetTempPath(),
        $"flow-control-tests-{Guid.NewGuid():N}");

    private readonly Action<IServiceCollection>? _configureServices = configureServices;

    public string DatabasePath => Path.Combine(_temporaryDirectory, "flow-control.db");
    public string ControllerDataPath => Path.Combine(_temporaryDirectory, "controllers.json");
    private string DatabaseConnectionString => $"Data Source={DatabasePath};Pooling=False";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Tests assert expected persistence failures directly. Avoid the Windows
        // Event Log provider, which requires machine-level write permission and
        // can otherwise replace the domain exception being asserted.
        builder.ConfigureLogging(logging => logging.ClearProviders());
        Directory.CreateDirectory(_temporaryDirectory);
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{DatabaseOptions.SectionName}:{DatabaseOptions.FlowControlConfigurationKey}"] = DatabaseConnectionString,
                [nameof(global::Server.Services.ServerOptions.CredentialEncryptionKey)] = TestCredentialEncryptionKey,
                [global::Server.Services.ServerOptions.ControllerDataFileConfigurationKey] = ControllerDataPath
            });
        });
        builder.ConfigureServices(services =>
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [$"{DatabaseOptions.SectionName}:{DatabaseOptions.FlowControlConfigurationKey}"] =
                        DatabaseConnectionString,
                    [nameof(global::Server.Services.ServerOptions.CredentialEncryptionKey)] =
                        TestCredentialEncryptionKey,
                    [global::Server.Services.ServerOptions.ControllerDataFileConfigurationKey] =
                        ControllerDataPath
                })
                .Build();
            services.AddFlowControlData(configuration);
            _configureServices?.Invoke(services);
        });
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IFlowVirtualMachineFactory>();
            services.AddSingleton<IFlowVirtualMachineFactory>(new TestFlowVirtualMachineFactory());
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }

    private sealed class TestFlowVirtualMachineFactory : IFlowVirtualMachineFactory
    {
        public IFlowVirtualMachine Create(ReadOnlyMemory<byte> artifact) => new TestFlowVirtualMachine();
    }

    private sealed class TestFlowVirtualMachine : IFlowVirtualMachine
    {
        private ulong _scanNumber;

        public FlowVmScanResult Scan(IReadOnlyList<FlowVmInput> inputs, ulong sampledAtMilliseconds) =>
            new(++_scanNumber, sampledAtMilliseconds, [true], []);

        public void Reset() => _scanNumber = 0;

        public void Dispose()
        {
        }
    }
}