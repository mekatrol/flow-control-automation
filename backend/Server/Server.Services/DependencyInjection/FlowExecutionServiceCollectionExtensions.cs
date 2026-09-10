using Server.Compiler.Services;
using Server.Services.FlowExecution.Debugging;
using Server.Services.FlowExecution.Deployment;
using Server.Services.FlowExecution.Emulation;
using Server.Services.FlowExecution.Runtime;
using Server.Services.FlowExecution.Simulation;
using Server.Services.FlowExecution.VirtualMachine;

namespace Server.Services.DependencyInjection;

internal static class FlowExecutionServiceCollectionExtensions
{
    internal static IServiceCollection AddFlowExecutionServices(this IServiceCollection services)
    {
        services.AddSingleton<IFlowVirtualMachineFactory, ManagedFlowVirtualMachineFactory>();
        services.AddSingleton<IFlowPointAdapter, ServerFlowPointAdapter>();
        services.AddSingleton<FlowRuntimeService>();
        services.AddSingleton<IFlowRuntimeService>(
            static provider => provider.GetRequiredService<FlowRuntimeService>());
        services.AddSingleton<IFlowRuntimeDeploymentService>(
            static provider => provider.GetRequiredService<FlowRuntimeService>());
        services.AddSingleton(provider => new FlowEmulatorService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<IFlowCompiler>(),
            provider.GetRequiredService<IFlowVirtualMachineFactory>(),
            provider.GetRequiredService<TimeProvider>()));
        services.AddSingleton<IFlowEmulatorService>(
            static provider => provider.GetRequiredService<FlowEmulatorService>());
        services.AddScoped<IFlowDeploymentService, FlowDeploymentService>();
        services.AddSingleton<FlowDebugSessionRegistry>();
        services.AddScoped<IFlowDebugService, FlowDebugService>();
        services.AddSingleton(provider => new FlowSimulatorSessionRegistry(
            provider.GetRequiredService<TimeProvider>(),
            TimeSpan.FromSeconds(provider.GetRequiredService<IOptions<FlowSimulatorOptions>>().Value.SessionLeaseSeconds)));
        services.AddScoped<IFlowSimulatorService, FlowSimulatorService>();

        return services;
    }
}