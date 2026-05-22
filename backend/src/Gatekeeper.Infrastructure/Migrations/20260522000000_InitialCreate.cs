using System;
using Gatekeeper.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gatekeeper.Infrastructure.Migrations;

[DbContext(typeof(GatekeeperDbContext))]
[Migration("20260522000000_InitialCreate")]
public sealed partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AccessRequests",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Intent = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                Requester = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                TargetsJson = table.Column<string>(type: "TEXT", nullable: false),
                RequestedCapabilitiesJson = table.Column<string>(type: "TEXT", nullable: false),
                DurationMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                Risk = table.Column<int>(type: "INTEGER", nullable: false),
                Justification = table.Column<string>(type: "TEXT", nullable: true),
                ProposedActionsJson = table.Column<string>(type: "TEXT", nullable: false),
                ForbiddenActionsJson = table.Column<string>(type: "TEXT", nullable: false),
                MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                Status = table.Column<int>(type: "INTEGER", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AccessRequests", accessRequest => accessRequest.Id);
            }
        );

        migrationBuilder.CreateTable(
            name: "AuditEvents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                EventType = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                AggregateId = table.Column<Guid>(type: "TEXT", nullable: true),
                OccurredAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuditEvents", auditEvent => auditEvent.Id);
            }
        );

        migrationBuilder.CreateIndex(
            name: "IX_AccessRequests_CreatedAt",
            table: "AccessRequests",
            column: "CreatedAt"
        );

        migrationBuilder.CreateIndex(
            name: "IX_AccessRequests_Status",
            table: "AccessRequests",
            column: "Status"
        );

        migrationBuilder.CreateIndex(
            name: "IX_AuditEvents_AggregateId",
            table: "AuditEvents",
            column: "AggregateId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_AuditEvents_OccurredAt",
            table: "AuditEvents",
            column: "OccurredAt"
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AccessRequests");
        migrationBuilder.DropTable(name: "AuditEvents");
    }
}
