using Microsoft.EntityFrameworkCore;
using Server.Data.Entities;

namespace Server.Data.Context;

public interface IFlowControlDbContext
{
    DbSet<FlowEntity> Flows { get; }

    DbSet<PointSourceEntity> PointSources { get; }

    DbSet<CredentialEntity> Credentials { get; }

    DbSet<TEntity> Set<TEntity>()
        where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    Task InitializeDatabase(CancellationToken cancellationToken = default);
}