using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Server.Data.Context;

#nullable disable

namespace Server.Data.Migrations;

[DbContext(typeof(FlowControlDbContext))]
[Migration("20260725010000_AddPointsAndGroups")]
internal sealed class AddPointsAndGroups : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        CreateEntityTable(migrationBuilder, "Points");
        CreateEntityTable(migrationBuilder, "PointGroups");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "PointGroups");
        migrationBuilder.DropTable(name: "Points");
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
                    defaultValue: 1),
            },
            constraints: table => table.PrimaryKey($"PK_{tableName}", item => item.Id));

        migrationBuilder.CreateIndex(
            name: $"IX_{tableName}_Key",
            table: tableName,
            column: "Key",
            unique: true);
    }
}