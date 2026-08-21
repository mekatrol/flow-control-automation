using Server.Data.Context;

namespace Server.Data.Migrations;

[DbContext(typeof(FlowControlDbContext))]
[Migration("20260821010000_AddVirtualPointRetainedStates")]
internal sealed class AddVirtualPointRetainedStates : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "VirtualPointRetainedStates",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", nullable: false),
                Key = table.Column<string>(type: "TEXT", nullable: false),
                Json = table.Column<string>(type: "TEXT", nullable: false),
                Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                Updated = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                RowVersion = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                ExecutionInstanceId = table.Column<string>(type: "TEXT", nullable: false),
                PointKey = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_VirtualPointRetainedStates", item => item.Id));
        migrationBuilder.CreateIndex("IX_VirtualPointRetainedStates_Key", "VirtualPointRetainedStates", "Key", unique: true);
        migrationBuilder.CreateIndex(
            "IX_VirtualPointRetainedStates_InstancePoint",
            "VirtualPointRetainedStates",
            ["ExecutionInstanceId", "PointKey"],
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "VirtualPointRetainedStates");
}