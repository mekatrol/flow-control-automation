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
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ServerAddress),
                "SERVER_ADDRESS must be non-empty.")
            .ValidateOnStart();

        services.AddFlowControlData(configuration);
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<FlowDatabaseService>();
        services.AddScoped<IFlowService>(
            static provider => provider.GetRequiredService<FlowDatabaseService>());
        services.AddScoped<IFlowStore>(
            static provider => provider.GetRequiredService<FlowDatabaseService>());
        return services;
    }
}