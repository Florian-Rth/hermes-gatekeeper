using Gatekeeper.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gatekeeper.Infrastructure.Migrations;

[DbContext(typeof(GatekeeperDbContext))]
[Migration("20260601100000_AddSessionSshProfileGrants")]
public sealed partial class AddSessionSshProfileGrants : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "SshProfileGrantsJson",
            table: "Sessions",
            type: "TEXT",
            nullable: false,
            defaultValue: "[]"
        );

        migrationBuilder.Sql(BuildBackfillSql(migrationBuilder.ActiveProvider));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "SshProfileGrantsJson", table: "Sessions");
    }

    private static string BuildBackfillSql(string activeProvider)
    {
        return activeProvider switch
        {
            "Microsoft.EntityFrameworkCore.Sqlite" => """
                UPDATE Sessions
                SET SshProfileGrantsJson = CASE
                    WHEN json_array_length(AllowedTargetsJson) = 1 THEN COALESCE(
                        (
                            SELECT json_group_array(
                                json_object(
                                    'targetAlias', json_extract(Sessions.AllowedTargetsJson, '$[0]'),
                                    'profileName', capability.value
                                )
                            )
                            FROM json_each(Sessions.AllowedCapabilitiesJson) AS capability
                            WHERE capability.value LIKE 'ssh.%' OR capability.value LIKE 'remote.%'
                        ),
                        '[]'
                    )
                    ELSE '[]'
                END;
                """,
            "Npgsql.EntityFrameworkCore.PostgreSQL" => """
                UPDATE "Sessions"
                SET "SshProfileGrantsJson" = CASE
                    WHEN jsonb_array_length("AllowedTargetsJson"::jsonb) = 1 THEN COALESCE(
                        (
                            SELECT jsonb_agg(
                                jsonb_build_object(
                                    'targetAlias', "AllowedTargetsJson"::jsonb ->> 0,
                                    'profileName', capability.value
                                )
                            )::text
                            FROM jsonb_array_elements_text("AllowedCapabilitiesJson"::jsonb) AS capability(value)
                            WHERE capability.value LIKE 'ssh.%' OR capability.value LIKE 'remote.%'
                        ),
                        '[]'
                    )
                    ELSE '[]'
                END;
                """,
            _ => "SELECT 1;",
        };
    }
}
