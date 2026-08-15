using Server.Data.Context;

#nullable disable

namespace Server.Data.Migrations;

[DbContext(typeof(FlowControlDbContext))]
[Migration("20260725000000_InitialCreate")]
internal sealed class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        CreateEntityTable(migrationBuilder, "Flows");
        CreateEntityTable(migrationBuilder, "PointSources");
        CreateEntityTable(migrationBuilder, "Credentials");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Credentials");
        migrationBuilder.DropTable(name: "Flows");
        migrationBuilder.DropTable(name: "PointSources");
    }

    private static void CreateEntityTable(MigrationBuilder migrationBuilder, string tableName)
    {
        migrationBuilder.CreateTable(
            name: tableName,
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", nullable: false),
                Key = table.Column<string>(type: "TEXT", nullable: false),
                Json = table.Column<string>(type: "TEXT", nullable: false),
                Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                Updated = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                RowVersion = table.Column<int>(
                    type: "INTEGER",
                    nullable: false,
                    defaultValue: 1)
            },
            constraints: table => table.PrimaryKey($"PK_{tableName}", item => item.Id));

        migrationBuilder.CreateIndex(
            name: $"IX_{tableName}_Key",
            table: tableName,
            column: "Key",
            unique: true);
    }
}