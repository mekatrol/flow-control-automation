using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Server.Data.Extensions;

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
        return services;
    }
}