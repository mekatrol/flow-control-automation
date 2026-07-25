using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Server.Data;
using Server.Data.Extensions;

namespace Tests.Unit.Api;

internal sealed class FlowControlApplicationFactory : WebApplicationFactory<Server.Api.Program>
{
    private const string TestCredentialEncryptionKey =
        "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=";

    private readonly string _temporaryDirectory = Path.Combine(
        Path.GetTempPath(),
        $"flow-control-tests-{Guid.NewGuid():N}");

    public string DatabasePath => Path.Combine(_temporaryDirectory, "flow-control.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_temporaryDirectory);
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{DatabaseOptions.SectionName}:{DatabaseOptions.FlowControlConfigurationKey}"] =
                    $"Data Source={DatabasePath}",
                [nameof(global::Server.Services.ServerOptions.CredentialEncryptionKey)] =
                    TestCredentialEncryptionKey,
            });
        });
        builder.ConfigureServices(services =>
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [$"{DatabaseOptions.SectionName}:{DatabaseOptions.FlowControlConfigurationKey}"] =
                        $"Data Source={DatabasePath}",
                    [nameof(global::Server.Services.ServerOptions.CredentialEncryptionKey)] =
                        TestCredentialEncryptionKey,
                })
                .Build();
            services.AddFlowControlData(configuration);
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
}