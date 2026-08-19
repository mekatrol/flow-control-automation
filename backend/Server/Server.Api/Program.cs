using Server.Api.Extensions;
using Server.Compiler.Extensions;
using Server.Services;
using Server.Services.Contracts;
using Server.Services.Extensions;

namespace Server.Api;

public partial class Program
{
    private const string LocalSettingsFileName = "appsettings.Local.json";

    private static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Configuration.AddJsonFile(
            LocalSettingsFileName,
            optional: true,
            reloadOnChange: true);

        var serverOptions = builder.Configuration.Get<ServerOptions>();
        if (builder.Configuration.GetSection(ServerOptions.AddressConfigurationKey).Exists()
            && serverOptions is not null)
        {
            builder.WebHost.UseUrls(serverOptions.ServerAddress);
        }

        builder.Services.AddFlowCompilerServices();
        builder.Services.AddFlowControlServer(builder.Configuration);
        builder.Services.ConfigureHttpJsonOptions(
            options => FlowControlJson.Configure(options.SerializerOptions));

        var app = builder.Build();

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IFlowControlDbContext>();
            await context.InitializeDatabase(app.Lifetime.ApplicationStopping);
            var dataValidator =
                scope.ServiceProvider.GetRequiredService<IStartupDataValidator>();
            await dataValidator.ValidateAsync(app.Lifetime.ApplicationStopping);
        }

        app.MapFlowControlEndpoints();
        await app.RunAsync();
    }
}