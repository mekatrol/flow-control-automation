using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Data.Migrations;

/// <inheritdoc />
public partial class AddFlowDebugLeaseAndExecutionMetadata : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "LastExecutedAt",
            table: "Flows",
            type: "TEXT",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "FlowDebugLeases",
            columns: table => new
            {
                FlowId = table.Column<string>(type: "TEXT", nullable: false),
                ExecutionContextId = table.Column<string>(type: "TEXT", nullable: false),
                StartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                LastHeartbeatAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                SuspendedEnabledDeployment = table.Column<bool>(type: "INTEGER", nullable: false),
                RowVersion = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FlowDebugLeases", item => item.FlowId);
                table.ForeignKey(
                    name: "FK_FlowDebugLeases_Flows_FlowId",
                    column: item => item.FlowId,
                    principalTable: "Flows",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_FlowDebugLeases_ExecutionContextId",
            table: "FlowDebugLeases",
            column: "ExecutionContextId",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "FlowDebugLeases");
        migrationBuilder.DropColumn(name: "LastExecutedAt", table: "Flows");
    }
}