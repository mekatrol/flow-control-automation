using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Data.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AuditRecords",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", nullable: false),
                Key = table.Column<string>(type: "TEXT", nullable: false),
                Json = table.Column<string>(type: "TEXT", nullable: false),
                Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                Updated = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                RowVersion = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1)
            },
            constraints: table => table.PrimaryKey("PK_AuditRecords", x => x.Id));

        migrationBuilder.CreateTable(
            name: "Credentials",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", nullable: false),
                Key = table.Column<string>(type: "TEXT", nullable: false),
                Json = table.Column<string>(type: "TEXT", nullable: false),
                Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                Updated = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                RowVersion = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1)
            },
            constraints: table => table.PrimaryKey("PK_Credentials", x => x.Id));

        migrationBuilder.CreateTable(
            name: "ExecutionContextDeployments",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", nullable: false),
                ExecutionContextId = table.Column<string>(type: "TEXT", nullable: false),
                ExecutionInstanceId = table.Column<string>(type: "TEXT", nullable: false),
                Key = table.Column<string>(type: "TEXT", nullable: false),
                Json = table.Column<string>(type: "TEXT", nullable: false),
                Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                Updated = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                RowVersion = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1)
            },
            constraints: table => table.PrimaryKey("PK_ExecutionContextDeployments", x => x.Id));

        migrationBuilder.CreateTable(
            name: "ExecutionContexts",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", nullable: false),
                Key = table.Column<string>(type: "TEXT", nullable: false),
                Json = table.Column<string>(type: "TEXT", nullable: false),
                Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                Updated = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                RowVersion = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1)
            },
            constraints: table => table.PrimaryKey("PK_ExecutionContexts", x => x.Id));

        migrationBuilder.CreateTable(
            name: "ExecutionInstances",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", nullable: false),
                Key = table.Column<string>(type: "TEXT", nullable: false),
                Json = table.Column<string>(type: "TEXT", nullable: false),
                Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                Updated = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                RowVersion = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1)
            },
            constraints: table => table.PrimaryKey("PK_ExecutionInstances", x => x.Id));

        migrationBuilder.CreateTable(
            name: "Flows",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", nullable: false),
                Key = table.Column<string>(type: "TEXT", nullable: false),
                Json = table.Column<string>(type: "TEXT", nullable: false),
                Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                Updated = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                RowVersion = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1)
            },
            constraints: table => table.PrimaryKey("PK_Flows", x => x.Id));

        migrationBuilder.CreateTable(
            name: "PointGroups",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", nullable: false),
                Key = table.Column<string>(type: "TEXT", nullable: false),
                Json = table.Column<string>(type: "TEXT", nullable: false),
                Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                Updated = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                RowVersion = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1)
            },
            constraints: table => table.PrimaryKey("PK_PointGroups", x => x.Id));

        migrationBuilder.CreateTable(
            name: "Points",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", nullable: false),
                Key = table.Column<string>(type: "TEXT", nullable: false),
                Json = table.Column<string>(type: "TEXT", nullable: false),
                Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                Updated = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                RowVersion = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1)
            },
            constraints: table => table.PrimaryKey("PK_Points", x => x.Id));

        migrationBuilder.CreateTable(
            name: "PointSources",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", nullable: false),
                Key = table.Column<string>(type: "TEXT", nullable: false),
                Json = table.Column<string>(type: "TEXT", nullable: false),
                Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                Updated = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                RowVersion = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1)
            },
            constraints: table => table.PrimaryKey("PK_PointSources", x => x.Id));

        migrationBuilder.CreateTable(
            name: "VirtualPointRetainedStates",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", nullable: false),
                ExecutionInstanceId = table.Column<string>(type: "TEXT", nullable: false),
                PointKey = table.Column<string>(type: "TEXT", nullable: false),
                Key = table.Column<string>(type: "TEXT", nullable: false),
                Json = table.Column<string>(type: "TEXT", nullable: false),
                Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                Updated = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                RowVersion = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1)
            },
            constraints: table => table.PrimaryKey("PK_VirtualPointRetainedStates", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_AuditRecords_Key",
            table: "AuditRecords",
            column: "Key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Credentials_Key",
            table: "Credentials",
            column: "Key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ExecutionContextDeployments_ExecutionContextId_ExecutionInstanceId",
            table: "ExecutionContextDeployments",
            columns: ["ExecutionContextId", "ExecutionInstanceId"],
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ExecutionContextDeployments_Key",
            table: "ExecutionContextDeployments",
            column: "Key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ExecutionContexts_Key",
            table: "ExecutionContexts",
            column: "Key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ExecutionInstances_Key",
            table: "ExecutionInstances",
            column: "Key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Flows_Key",
            table: "Flows",
            column: "Key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PointGroups_Key",
            table: "PointGroups",
            column: "Key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Points_Key",
            table: "Points",
            column: "Key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PointSources_Key",
            table: "PointSources",
            column: "Key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_VirtualPointRetainedStates_ExecutionInstanceId_PointKey",
            table: "VirtualPointRetainedStates",
            columns: ["ExecutionInstanceId", "PointKey"],
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_VirtualPointRetainedStates_Key",
            table: "VirtualPointRetainedStates",
            column: "Key",
            unique: true);

        // The built-in execution instance is required on every fresh installation.
        migrationBuilder.InsertData(
            table: "ExecutionInstances",
            columns: ["Id", "Key", "Json", "Created", "Updated", "RowVersion"],
            columnTypes: ["TEXT", "TEXT", "TEXT", "TEXT", "TEXT", "INTEGER"],
            values:
            [
                "server", "server",
                """{"id":"server","name":"Built-in server","executionInstanceType":"server","enabled":true,"revision":1}""",
                DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, 1
            ]);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AuditRecords");

        migrationBuilder.DropTable(
            name: "Credentials");

        migrationBuilder.DropTable(
            name: "ExecutionContextDeployments");

        migrationBuilder.DropTable(
            name: "ExecutionContexts");

        migrationBuilder.DropTable(
            name: "ExecutionInstances");

        migrationBuilder.DropTable(
            name: "Flows");

        migrationBuilder.DropTable(
            name: "PointGroups");

        migrationBuilder.DropTable(
            name: "Points");

        migrationBuilder.DropTable(
            name: "PointSources");

        migrationBuilder.DropTable(
            name: "VirtualPointRetainedStates");
    }
}