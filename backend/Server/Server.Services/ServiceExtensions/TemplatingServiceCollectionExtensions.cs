using Server.Common.Contracts.Templating;
using Server.Services.Templating;

namespace Server.Services.ServiceExtensions;

internal static class TemplatingServiceCollectionExtensions
{
    internal static IServiceCollection AddTemplatingServices(this IServiceCollection services)
    {
        services.AddSingleton<ITemplateService, ScribanTemplateService>();

        return services;
    }
}