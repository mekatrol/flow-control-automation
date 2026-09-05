using Server.Data.Context;

namespace Server.Data.Migrations;

[DbContext(typeof(FlowControlDbContext))]
[Migration("20260905000000_RenameFlowNodeType")]
internal sealed class RenameFlowNodeType : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => Rename(migrationBuilder, "kind", "nodeType");

    protected override void Down(MigrationBuilder migrationBuilder) => Rename(migrationBuilder, "nodeType", "kind");

    private static void Rename(MigrationBuilder migrationBuilder, string oldName, string newName)
    {
        // Only node discriminators change. Configuration keys and other JSON remain intact.
        foreach (var path in new[] { "$.nodes", "$.deployedVersion.nodes" })
        {
            migrationBuilder.Sql($"""
                UPDATE Flows
                SET Json = json_set(Json, '{path}', json((
                    SELECT json_group_array(json(
                        CASE WHEN json_type(value, '$.{oldName}') IS NOT NULL
                        THEN json_remove(
                            CASE WHEN json_type(value, '$.{newName}') IS NULL
                            THEN json_set(value, '$.{newName}', json_extract(value, '$.{oldName}'))
                            ELSE value END,
                            '$.{oldName}')
                        ELSE value END
                    ))
                    FROM json_each(Flows.Json, '{path}')
                )))
                WHERE EXISTS (
                    SELECT 1 FROM json_each(Flows.Json, '{path}')
                    WHERE json_type(value, '$.{oldName}') IS NOT NULL
                );
                """);
        }
    }
}
