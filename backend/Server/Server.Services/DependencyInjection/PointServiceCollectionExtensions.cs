using Server.Services.Points.Definitions;
using Server.Services.Points.Runtime;
using Server.Services.Points.Sources;
using Server.Services.Points.Validation;

namespace Server.Services.DependencyInjection;

internal static class PointServiceCollectionExtensions
{
    internal static IServiceCollection AddPointServices(this IServiceCollection services)
    {
        services.AddSingleton<IVirtualPointRetainedStore, VirtualPointRetainedDatabaseStore>();
        services.AddSingleton<IVirtualPointRuntimeStore, VirtualPointRuntimeStore>();
        services.AddSingleton<IPointSourceValidator, PointSourceValidator>();
        services.AddSingleton<IPointDefinitionValidator, PointDefinitionValidator>();
        services.AddScoped<IPointDefinitionStore, PointDefinitionDatabaseStore>();
        services.AddScoped<IPointReadService, PointReadService>();
        services.AddScoped<IPointSourceService, PointSourceDatabaseService>();

        return services;
    }
}