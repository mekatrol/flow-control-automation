using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Server.Data.Extensions;
using Server.Services.Implementation;

namespace Server.Services.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFlowControlServer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<ServerOptions>()
            .Bind(configuration)
            .Configure(options =>
            {
                // Keep the deployment-compatible uppercase environment variable
                // while appsettings files bind through the idiomatic property name.
                var environmentKey =
                    configuration[ServerOptions.CredentialEncryptionKeyConfigurationKey];
                if (!string.IsNullOrWhiteSpace(environmentKey))
                {
                    options.CredentialEncryptionKey = environmentKey;
                }
            })
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ServerAddress),
                "SERVER_ADDRESS must be non-empty.")
            .Validate(
                ServerOptions.HasValidCredentialEncryptionKey,
                "CREDENTIAL_ENCRYPTION_KEY must be Base64 for exactly 32 bytes.")
            .ValidateOnStart();

        services.AddFlowControlData(configuration);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IFlowRuntimeService, FlowRuntimeService>();
        services.AddScoped<FlowDatabaseService>();
        services.AddScoped<IFlowService>(
            static provider => provider.GetRequiredService<FlowDatabaseService>());
        services.AddScoped<IFlowStore>(
            static provider => provider.GetRequiredService<FlowDatabaseService>());
        services.AddScoped<IPointSourceService, PointSourceDatabaseService>();
        services.AddScoped<CredentialDatabaseService>();
        services.AddScoped<ICredentialStore>(
            static provider => provider.GetRequiredService<CredentialDatabaseService>());
        services.AddScoped<ICredentialResolver>(
            static provider => provider.GetRequiredService<CredentialDatabaseService>());
        return services;
    }
}