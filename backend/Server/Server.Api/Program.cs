using Server.Api.Extensions;
using Server.Data.Context;
using Server.Services;
using Server.Services.Extensions;

namespace Server.Api;

public partial class Program
{
    private static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var serverOptions = builder.Configuration.Get<ServerOptions>();
        if (builder.Configuration.GetSection(ServerOptions.AddressConfigurationKey).Exists()
            && serverOptions is not null)
        {
            builder.WebHost.UseUrls(serverOptions.ServerAddress);
        }

        builder.Services.AddFlowControlServer(builder.Configuration);

        var app = builder.Build();

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IFlowControlDbContext>();
            await context.InitializeDatabase(app.Lifetime.ApplicationStopping);
        }

        app.MapFlowControlEndpoints();
        await app.RunAsync();
    }
}