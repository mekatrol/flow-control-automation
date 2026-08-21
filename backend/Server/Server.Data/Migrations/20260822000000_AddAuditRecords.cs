using Server.Data.Context;

namespace Server.Data.Migrations;

[DbContext(typeof(FlowControlDbContext))]
[Migration("20260822000000_AddAuditRecords")]
internal sealed class AddAuditRecords : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("AuditRecords", table => new
        {
            Id = table.Column<string>(type: "TEXT", nullable: false),
            Key = table.Column<string>(type: "TEXT", nullable: false),
            Json = table.Column<string>(type: "TEXT", nullable: false),
            Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
            Updated = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
            RowVersion = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1)
        }, constraints: table => table.PrimaryKey("PK_AuditRecords", item => item.Id));
        migrationBuilder.CreateIndex("IX_AuditRecords_Key", "AuditRecords", "Key", unique: true);
    }
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("AuditRecords");
}