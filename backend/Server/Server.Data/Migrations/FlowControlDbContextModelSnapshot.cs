using Server.Data.Context;
using Server.Data.Entities;

#nullable disable

namespace Server.Data.Migrations;

[DbContext(typeof(FlowControlDbContext))]
internal sealed class FlowControlDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.10");

        BuildEntity<CredentialEntity>(modelBuilder, "Credentials");
        BuildEntity<FlowEntity>(modelBuilder, "Flows");
        BuildEntity<PointSourceEntity>(modelBuilder, "PointSources");
        BuildEntity<PointEntity>(modelBuilder, "Points");
        BuildEntity<PointGroupEntity>(modelBuilder, "PointGroups");
    }

    private static void BuildEntity<TEntity>(ModelBuilder modelBuilder, string tableName)
        where TEntity : BaseEntity
    {
        modelBuilder.Entity<TEntity>(entity =>
        {
            entity.Property(item => item.Id).HasColumnType("TEXT");
            entity.Property(item => item.Created).HasColumnType("TEXT");
            entity.Property(item => item.Json).IsRequired().HasColumnType("TEXT");
            entity.Property(item => item.Key).IsRequired().HasColumnType("TEXT");
            entity.Property(item => item.RowVersion)
                .IsConcurrencyToken()
                .ValueGeneratedOnAdd()
                .HasColumnType("INTEGER")
                .HasDefaultValue(1);
            entity.Property(item => item.Updated).HasColumnType("TEXT");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.Key).IsUnique();
            entity.ToTable(tableName);
        });
    }
}
