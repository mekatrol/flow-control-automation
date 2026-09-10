namespace Server.Services.DependencyInjection;

internal static class ServiceOptionsServiceCollectionExtensions
{
    internal static IServiceCollection AddServerServiceOptions(
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

                var controllerDataFile =
                    configuration[ServerOptions.ControllerDataFileConfigurationKey];

                if (!string.IsNullOrWhiteSpace(controllerDataFile))
                {
                    options.ControllerDataFile = controllerDataFile;
                }
            })
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ServerAddress),
                "SERVER_ADDRESS must be non-empty.")
            .Validate(
                ServerOptions.HasValidCredentialEncryptionKey,
                "CREDENTIAL_ENCRYPTION_KEY must be Base64 for exactly 32 bytes.")
            .ValidateOnStart();

        services
            .AddOptions<FlowSimulatorOptions>()
            .Bind(configuration.GetSection(FlowSimulatorOptions.SectionName))
            .Validate(
                options => options.SessionLeaseSeconds > 0,
                "FlowSimulator:SessionLeaseSeconds must be greater than zero.")
            .ValidateOnStart();

        return services;
    }
}