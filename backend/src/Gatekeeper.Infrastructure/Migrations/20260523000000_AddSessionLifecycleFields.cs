using System;
using Gatekeeper.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gatekeeper.Infrastructure.Migrations;

[DbContext(typeof(GatekeeperDbContext))]
[Migration("20260523000000_AddSessionLifecycleFields")]
public sealed partial class AddSessionLifecycleFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        string dateTimeOffsetType =
            migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL"
                ? "timestamp with time zone"
                : "TEXT";

        migrationBuilder.AddColumn<int>(
            name: "ActionCount",
            table: "Sessions",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0
        );

        migrationBuilder.AddColumn<int>(
            name: "MaxActionCount",
            table: "Sessions",
            type: "INTEGER",
            nullable: false,
            defaultValue: 10
        );

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "CompletedAt",
            table: "Sessions",
            type: dateTimeOffsetType,
            nullable: true
        );

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "RevokedAt",
            table: "Sessions",
            type: dateTimeOffsetType,
            nullable: true
        );

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "ExpiredAt",
            table: "Sessions",
            type: dateTimeOffsetType,
            nullable: true
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ActionCount", table: "Sessions");
        migrationBuilder.DropColumn(name: "MaxActionCount", table: "Sessions");
        migrationBuilder.DropColumn(name: "CompletedAt", table: "Sessions");
        migrationBuilder.DropColumn(name: "RevokedAt", table: "Sessions");
        migrationBuilder.DropColumn(name: "ExpiredAt", table: "Sessions");
    }
}
