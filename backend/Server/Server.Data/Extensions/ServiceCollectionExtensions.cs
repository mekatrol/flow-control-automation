using Server.Data.Context;

namespace Server.Data.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFlowControlData(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ConnectionString),
                "The flow-control database connection string must be non-empty.")
            .ValidateOnStart();

        services.AddDbContext<FlowControlDbContext>((provider, options) =>
            options.UseSqlite(
                provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString));
        services.AddScoped<IFlowControlDbContext>(
            static provider => provider.GetRequiredService<FlowControlDbContext>());
        return services;
    }
}