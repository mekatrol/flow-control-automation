using Server.Services.Validation.Startup;

namespace Server.Services.ServiceExtensions;

internal static class StartupValidationServiceCollectionExtensions
{
    internal static IServiceCollection AddStartupValidationServices(this IServiceCollection services)
    {
        services.AddScoped<IStartupDataValidator, StartupDataValidator>();

        return services;
    }
}