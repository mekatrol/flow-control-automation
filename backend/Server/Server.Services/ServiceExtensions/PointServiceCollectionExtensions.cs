using Server.Services.Points.Definitions;
using Server.Services.Points.Runtime;
using Server.Services.Points.Sources;
using Server.Services.Points.Validation;

namespace Server.Services.ServiceExtensions;

internal static class PointServiceCollectionExtensions
{
    internal static IServiceCollection AddPointServices(this IServiceCollection services)
    {
        services.AddSingleton<IVirtualPointRetainedStore, VirtualPointRetainedDatabaseStore>();
        services.AddSingleton<IVirtualPointRuntimeStore, VirtualPointRuntimeStore>();
        services.AddSingleton<IPointSourceValidator, PointSourceValidator>();
        services.AddSingleton<IPointMappingResolver, PointMappingResolver>();
        services.AddSingleton<IPointValueConverter, PointValueConverter>();
        services.AddScoped<IPointMappingExecutionService, PointMappingExecutionService>();
        services.AddScoped<IPointMappingAdapter, HttpJsonPointMappingAdapter>();
        services.AddScoped<IPointMappingAdapter, MqttPointMappingAdapter>();
        services.AddScoped<IPointMappingAdapter, HomeAssistantPointMappingAdapter>();
        services.AddScoped<IPointMappingAdapter, PhysicalPointMappingAdapter>();
        services.AddScoped<IPointMappingAdapter, VirtualPointMappingAdapter>();
        services.AddSingleton<IPointDefinitionValidator, PointDefinitionValidator>();
        services.AddScoped<IPointDefinitionStore, PointDefinitionDatabaseStore>();
        services.AddScoped<IPointReadService, PointReadService>();
        services.AddScoped<IPointSourceService, PointSourceDatabaseService>();

        return services;
    }
}