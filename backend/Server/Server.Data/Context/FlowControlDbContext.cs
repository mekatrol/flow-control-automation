using Microsoft.EntityFrameworkCore;
using Server.Data.Entities;

namespace Server.Data.Context;

internal sealed class FlowControlDbContext(DbContextOptions<FlowControlDbContext> options)
    : DbContext(options), IFlowControlDbContext
{
    private static readonly string[] TableNames = ["Flows", "PointSources", "Credentials"];

    public DbSet<FlowEntity> Flows => Set<FlowEntity>();

    public DbSet<PointSourceEntity> PointSources => Set<PointSourceEntity>();

    public DbSet<CredentialEntity> Credentials => Set<CredentialEntity>();

    public async Task InitializeDatabase(CancellationToken cancellationToken = default)
    {
        await Database.MigrateAsync(cancellationToken);

        // SQLite does not generate row versions. These idempotent triggers make
        // the integer token useful for optimistic concurrency across processes.
        foreach (var tableName in TableNames)
        {
            var sql = $"""
                CREATE TRIGGER IF NOT EXISTS UpdateRowVersion{tableName}
                AFTER UPDATE ON {tableName}
                BEGIN
                    UPDATE {tableName}
                    SET RowVersion = RowVersion + 1
                    WHERE rowid = NEW.rowid;
                END;
                """;
            await Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureEntity(modelBuilder.Entity<FlowEntity>());
        ConfigureEntity(modelBuilder.Entity<PointSourceEntity>());
        ConfigureEntity(modelBuilder.Entity<CredentialEntity>());
    }

    private static void ConfigureEntity<TEntity>(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> entity)
        where TEntity : BaseEntity
    {
        entity.HasKey(item => item.Id);
        entity.HasIndex(item => item.Key).IsUnique();
        entity.Property(item => item.Id).IsRequired();
        entity.Property(item => item.Key).IsRequired();
        entity.Property(item => item.Json).IsRequired();
        entity.Property(item => item.RowVersion).HasDefaultValue(1).IsConcurrencyToken();
    }
}