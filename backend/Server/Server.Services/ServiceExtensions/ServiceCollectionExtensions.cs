using Server.Data.Extensions;

namespace Server.Services.ServiceExtensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddServerServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddServerServiceOptions(configuration);
        services.AddFlowControlData(configuration);
        services.AddSingleton(TimeProvider.System);
        services.AddAuditServices();
        services.AddCommunicationServices();
        services.AddConfigurationServices();
        services.AddFlowExecutionServices();
        services.AddPointServices();
        services.AddTemplatingServices();
        services.AddStartupValidationServices();

        return services;
    }
}