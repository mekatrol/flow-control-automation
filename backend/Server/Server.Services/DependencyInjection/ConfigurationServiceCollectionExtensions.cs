using Server.Services.Configuration.ControllerTemplates;
using Server.Services.Configuration.Credentials;
using Server.Services.Configuration.Execution;
using Server.Services.Configuration.Flows;

namespace Server.Services.DependencyInjection;

internal static class ConfigurationServiceCollectionExtensions
{
    internal static IServiceCollection AddConfigurationServices(this IServiceCollection services)
    {
        services.AddScoped<FlowDatabaseService>();
        services.AddScoped<IFlowService>(
            static provider => provider.GetRequiredService<FlowDatabaseService>());
        services.AddScoped<IFlowStore>(
            static provider => provider.GetRequiredService<FlowDatabaseService>());
        services.AddSingleton<IControllerTemplateValidator, ControllerTemplateValidator>();
        services.AddSingleton<IControllerTemplateStore, ControllerTemplateFileStore>();
        services.AddScoped<CredentialDatabaseService>();
        services.AddScoped<ICredentialStore>(
            static provider => provider.GetRequiredService<CredentialDatabaseService>());
        services.AddScoped<ICredentialResolver>(
            static provider => provider.GetRequiredService<CredentialDatabaseService>());
        services.AddScoped<IExecutionConfigurationService, ExecutionConfigurationService>();

        return services;
    }
}