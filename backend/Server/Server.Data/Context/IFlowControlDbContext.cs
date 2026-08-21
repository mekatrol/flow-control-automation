using Server.Data.Entities;

namespace Server.Data.Context;

public interface IFlowControlDbContext
{
    DbSet<FlowEntity> Flows { get; }

    DbSet<PointSourceEntity> PointSources { get; }

    DbSet<PointEntity> Points { get; }

    DbSet<PointGroupEntity> PointGroups { get; }

    DbSet<CredentialEntity> Credentials { get; }

    DbSet<ExecutionContextEntity> ExecutionContexts => Set<ExecutionContextEntity>();

    DbSet<ExecutionInstanceEntity> ExecutionInstances => Set<ExecutionInstanceEntity>();

    DbSet<ExecutionContextDeploymentEntity> ExecutionContextDeployments => Set<ExecutionContextDeploymentEntity>();

    DbSet<VirtualPointRetainedStateEntity> VirtualPointRetainedStates => Set<VirtualPointRetainedStateEntity>();
    DbSet<AuditRecordEntity> AuditRecords => Set<AuditRecordEntity>();

    DbSet<TEntity> Set<TEntity>()
        where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    Task ReloadAsync<TEntity>(TEntity entity, CancellationToken cancellationToken)
        where TEntity : class;

    Task InitializeDatabase(CancellationToken cancellationToken = default);
}