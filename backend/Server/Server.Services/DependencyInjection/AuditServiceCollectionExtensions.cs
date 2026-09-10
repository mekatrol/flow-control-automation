using Server.Services.Audit;

namespace Server.Services.DependencyInjection;

internal static class AuditServiceCollectionExtensions
{
    internal static IServiceCollection AddAuditServices(this IServiceCollection services)
    {
        services.AddSingleton<IAuditService, AuditService>();

        return services;
    }
}