using Server.Data.Context;

#nullable disable

namespace Server.Data.Migrations;

[DbContext(typeof(FlowControlDbContext))]
[Migration("20260821000000_AddExecutionContextsAndInstances")]
internal sealed class AddExecutionContextsAndInstances : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        CreateEntityTable(migrationBuilder, "ExecutionContexts");
        CreateEntityTable(migrationBuilder, "ExecutionInstances");
        CreateDeploymentTable(migrationBuilder);
        migrationBuilder.InsertData(
            table: "ExecutionInstances",
            columns: ["Id", "Key", "Json", "Created", "Updated", "RowVersion"],
            columnTypes: ["TEXT", "TEXT", "TEXT", "TEXT", "TEXT", "INTEGER"],
            values: new object[]
            {
                "server", "server",
                "{\"id\":\"server\",\"name\":\"Built-in server\",\"kind\":\"server\",\"enabled\":true,\"revision\":1}",
                DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, 1
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ExecutionContextDeployments");
        migrationBuilder.DropTable(name: "ExecutionInstances");
        migrationBuilder.DropTable(name: "ExecutionContexts");
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
                RowVersion = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1)
            },
            constraints: table => table.PrimaryKey($"PK_{tableName}", item => item.Id));

        migrationBuilder.CreateIndex($"IX_{tableName}_Key", tableName, "Key", unique: true);
    }

    private static void CreateDeploymentTable(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ExecutionContextDeployments",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", nullable: false),
                Key = table.Column<string>(type: "TEXT", nullable: false),
                Json = table.Column<string>(type: "TEXT", nullable: false),
                Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                Updated = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                RowVersion = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                ExecutionContextId = table.Column<string>(type: "TEXT", nullable: false),
                ExecutionInstanceId = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_ExecutionContextDeployments", item => item.Id));
        migrationBuilder.CreateIndex("IX_ExecutionContextDeployments_Key", "ExecutionContextDeployments", "Key", unique: true);
        migrationBuilder.CreateIndex(
            "IX_ExecutionContextDeployments_ExecutionContextId_ExecutionInstanceId",
            "ExecutionContextDeployments",
            ["ExecutionContextId", "ExecutionInstanceId"],
            unique: true);
    }
}
