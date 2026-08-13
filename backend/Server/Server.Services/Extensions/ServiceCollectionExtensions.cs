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

        services.AddFlowControlData(configuration);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IFlowVirtualMachineFactory, NativeFlowVirtualMachineFactory>();
        services.AddSingleton<IFlowPointAdapter, ServerFlowPointAdapter>();
        services.AddSingleton<FlowRuntimeService>();
        services.AddSingleton<IFlowRuntimeService>(provider => provider.GetRequiredService<FlowRuntimeService>());
        services.AddSingleton<FlowEmulatorService>(provider => new FlowEmulatorService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<IFlowCompiler>(),
            provider.GetRequiredService<IFlowVirtualMachineFactory>()));
        services.AddSingleton<IFlowEmulatorService>(provider => provider.GetRequiredService<FlowEmulatorService>());
        services.AddScoped<IFlowDeploymentService, FlowDeploymentService>();
        services.AddScoped<FlowDatabaseService>();
        services.AddScoped<IFlowService>(
            static provider => provider.GetRequiredService<FlowDatabaseService>());
        services.AddScoped<IFlowStore>(
            static provider => provider.GetRequiredService<FlowDatabaseService>());
        services.AddSingleton<IPointSourceValidator, PointSourceValidator>();
        services.AddSingleton<IPointDefinitionValidator, PointDefinitionValidator>();
        services.AddSingleton<IControllerTemplateValidator, ControllerTemplateValidator>();
        services.AddSingleton<IControllerTemplateStore, ControllerTemplateFileStore>();
        services.AddScoped<IPointDefinitionStore, PointDefinitionDatabaseStore>();
        services.AddScoped<IFlowCompilationTargetResolver, FlowCompilationTargetResolver>();
        services.AddSingleton<IFlowCompiler, FlowCompiler>();
        services.AddSingleton<IFlowDecompiler, FlowDecompiler>();
        services.AddSingleton<IFcpClient, UnavailableFcpClient>();
        services.AddSingleton<IControllerDebugTransport, FcpControllerDebugTransport>();
        services.AddSingleton<FlowDebugSessionRegistry>();
        services.AddScoped<IFlowDebugService, FlowDebugService>();
        services.AddScoped<IPointReadService, PointReadService>();
        services.AddScoped<IPointSourceService, PointSourceDatabaseService>();
        services.AddScoped<CredentialDatabaseService>();
        services.AddScoped<ICredentialStore>(
            static provider => provider.GetRequiredService<CredentialDatabaseService>());
        services.AddScoped<ICredentialResolver>(
            static provider => provider.GetRequiredService<CredentialDatabaseService>());
        services.AddSingleton<IConnectivityClock, ConnectivityClock>();
        services.AddSingleton<ConnectivityRateLimiter>();
        services.AddSingleton<IDnsLookup, DnsLookup>();
        services.AddSingleton<ITcpConnectionFactory, TcpConnectionFactory>();
        services.AddSingleton<ITlsHandshake, TlsHandshake>();
        services.AddSingleton<IHttpProtocolCheck, HttpProtocolCheck>();
        services.AddSingleton<IMqttProtocolCheck, MqttProtocolCheck>();
        services.AddScoped<IConnectivityService, ConnectivityService>();
        services.AddScoped<IStartupDataValidator, StartupDataValidator>();
        return services;
    }
}
