using Microsoft.Extensions.DependencyInjection;
using Server.Compiler.Services;
using Server.Compiler.Services.Implementation;

namespace Server.Compiler.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFlowCompilerServices(this IServiceCollection services)
    {
        services.AddSingleton<IFlowValidator, FlowValidator>();
        services.AddSingleton<IFlowCompiler, FlowCompiler>();
        services.AddSingleton<IFlowDecompiler, FlowDecompiler>();
        services.AddScoped<IFlowCompilationTargetResolver, FlowCompilationTargetResolver>();

        return services;
    }
}