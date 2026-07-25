using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Server.Data;
using Server.Data.Context;
using Server.Data.Entities;
using Server.Services;
using Server.Services.Extensions;

namespace Tests.Unit.Api;

public sealed class RegistrationTests
{
    [Test]
    public void PublicRegistrationCanBeOverriddenThroughItsInterface()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{DatabaseOptions.SectionName}:{DatabaseOptions.FlowControlConfigurationKey}"] =
                    "Data Source=:memory:",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddFlowControlServer(configuration);
        services.AddScoped<IFlowControlDbContext, FakeContext>();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<IFlowControlDbContext>();

        Assert.That(context, Is.TypeOf<FakeContext>());
    }

    [Test]
    public void RegistrationBindsConfigurationModels()
    {
        const string address = "http://localhost:9876";
        const string encryptionKey =
            "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=";
        const string connectionString = "Data Source=bound.db";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [ServerOptions.AddressConfigurationKey] = address,
                [ServerOptions.CredentialEncryptionKeyConfigurationKey] = encryptionKey,
                [$"{DatabaseOptions.SectionName}:{DatabaseOptions.FlowControlConfigurationKey}"] =
                    connectionString,
            })
            .Build();
        var services = new ServiceCollection();
        services.AddFlowControlServer(configuration);
        using var provider = services.BuildServiceProvider();

        var server = provider.GetRequiredService<IOptions<ServerOptions>>().Value;
        var database = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;

        Assert.Multiple(() =>
        {
            Assert.That(server.ServerAddress, Is.EqualTo(address));
            Assert.That(server.CredentialEncryptionKey, Is.EqualTo(encryptionKey));
            Assert.That(database.ConnectionString, Is.EqualTo(connectionString));
        });
    }

    private sealed class FakeContext : IFlowControlDbContext
    {
        public DbSet<FlowEntity> Flows => throw new NotSupportedException();

        public DbSet<PointSourceEntity> PointSources => throw new NotSupportedException();

        public DbSet<PointEntity> Points => throw new NotSupportedException();

        public DbSet<PointGroupEntity> PointGroups => throw new NotSupportedException();

        public DbSet<CredentialEntity> Credentials => throw new NotSupportedException();

        public DbSet<TEntity> Set<TEntity>()
            where TEntity : class => throw new NotSupportedException();

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task ReloadAsync<TEntity>(
            TEntity entity,
            CancellationToken cancellationToken)
            where TEntity : class => throw new NotSupportedException();

        public Task InitializeDatabase(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}