using Server.Services.DependencyInjection;

namespace Tests.Unit.Helpers;

internal static class TestServices
{
    public static ServiceProvider CreateProvider(
        Action<IServiceCollection>? configure = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:FlowControl"] = "Data Source=:memory:"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddServerServices(configuration);
        configure?.Invoke(services);

        return services.BuildServiceProvider();
    }
}