using System;
using Gatekeeper.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gatekeeper.Infrastructure.Migrations;

[DbContext(typeof(GatekeeperDbContext))]
[Migration("20260522010000_AddSessions")]
public sealed partial class AddSessions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Sessions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                AccessRequestId = table.Column<Guid>(type: "TEXT", nullable: false),
                Status = table.Column<int>(type: "INTEGER", nullable: false),
                AllowedTargetsJson = table.Column<string>(type: "TEXT", nullable: false),
                AllowedCapabilitiesJson = table.Column<string>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Sessions", session => session.Id);
            }
        );

        migrationBuilder.CreateIndex(
            name: "IX_Sessions_AccessRequestId",
            table: "Sessions",
            column: "AccessRequestId",
            unique: true
        );

        migrationBuilder.CreateIndex(
            name: "IX_Sessions_ExpiresAt",
            table: "Sessions",
            column: "ExpiresAt"
        );

        migrationBuilder.CreateIndex(
            name: "IX_Sessions_Status",
            table: "Sessions",
            column: "Status"
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Sessions");
    }
}
