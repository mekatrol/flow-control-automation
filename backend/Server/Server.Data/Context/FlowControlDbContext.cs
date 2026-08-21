using Server.Data.Entities;

namespace Server.Data.Context;

internal sealed class FlowControlDbContext(DbContextOptions<FlowControlDbContext> options)
    : DbContext(options), IFlowControlDbContext
{
    private static readonly string[] TableNames =
        [nameof(Flows), nameof(PointSources), nameof(Points), nameof(PointGroups), nameof(Credentials),
            nameof(ExecutionContexts), nameof(ExecutionInstances), nameof(ExecutionContextDeployments),
            nameof(VirtualPointRetainedStates), nameof(AuditRecords)];

    public DbSet<FlowEntity> Flows => Set<FlowEntity>();

    public DbSet<PointSourceEntity> PointSources => Set<PointSourceEntity>();

    public DbSet<PointEntity> Points => Set<PointEntity>();

    public DbSet<PointGroupEntity> PointGroups => Set<PointGroupEntity>();

    public DbSet<CredentialEntity> Credentials => Set<CredentialEntity>();

    public DbSet<ExecutionContextEntity> ExecutionContexts => Set<ExecutionContextEntity>();

    public DbSet<ExecutionInstanceEntity> ExecutionInstances => Set<ExecutionInstanceEntity>();

    public DbSet<ExecutionContextDeploymentEntity> ExecutionContextDeployments => Set<ExecutionContextDeploymentEntity>();

    public DbSet<VirtualPointRetainedStateEntity> VirtualPointRetainedStates => Set<VirtualPointRetainedStateEntity>();
    public DbSet<AuditRecordEntity> AuditRecords => Set<AuditRecordEntity>();

    public Task ReloadAsync<TEntity>(TEntity entity, CancellationToken cancellationToken)
        where TEntity : class =>
        Entry(entity).ReloadAsync(cancellationToken);

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
        ConfigureEntity(modelBuilder.Entity<PointEntity>());
        ConfigureEntity(modelBuilder.Entity<PointGroupEntity>());
        ConfigureEntity(modelBuilder.Entity<CredentialEntity>());
        ConfigureEntity(modelBuilder.Entity<ExecutionContextEntity>());
        ConfigureEntity(modelBuilder.Entity<ExecutionInstanceEntity>());
        ConfigureEntity(modelBuilder.Entity<ExecutionContextDeploymentEntity>());
        ConfigureEntity(modelBuilder.Entity<VirtualPointRetainedStateEntity>());
        ConfigureEntity(modelBuilder.Entity<AuditRecordEntity>());
        modelBuilder.Entity<ExecutionContextDeploymentEntity>()
            .HasIndex(item => new { item.ExecutionContextId, item.ExecutionInstanceId })
            .IsUnique();
        modelBuilder.Entity<VirtualPointRetainedStateEntity>()
            .HasIndex(item => new { item.ExecutionInstanceId, item.PointKey })
            .IsUnique();
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