using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Server.Data.Context;

#nullable disable

namespace Server.Data.Migrations;

[DbContext(typeof(FlowControlDbContext))]
[Migration("20260815000000_AddFlowScenarios")]
internal sealed class AddFlowScenarios : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "FlowScenarios",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", nullable: false),
                Key = table.Column<string>(type: "TEXT", nullable: false),
                Json = table.Column<string>(type: "TEXT", nullable: false),
                Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                Updated = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                RowVersion = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1)
            },
            constraints: table => table.PrimaryKey("PK_FlowScenarios", item => item.Id));
        migrationBuilder.CreateIndex(name: "IX_FlowScenarios_Key", table: "FlowScenarios", column: "Key");
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "FlowScenarios");
}
