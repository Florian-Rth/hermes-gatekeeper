using Gatekeeper.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gatekeeper.Infrastructure.Migrations;

[DbContext(typeof(GatekeeperDbContext))]
[Migration("20260603120000_AddSshCatalog")]
public sealed partial class AddSshCatalog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SshTargets",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                Alias = table.Column<string>(maxLength: 200, nullable: false),
                Host = table.Column<string>(maxLength: 500, nullable: false),
                Port = table.Column<int>(nullable: false),
                Username = table.Column<string>(maxLength: 200, nullable: false),
                PrivateKeyPath = table.Column<string>(maxLength: 1000, nullable: false),
                KnownHostsPath = table.Column<string>(maxLength: 1000, nullable: false),
                DefaultTimeoutSeconds = table.Column<int>(nullable: false),
                DefaultOutputLimitBytes = table.Column<int>(nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SshTargets", x => x.Id);
            }
        );

        migrationBuilder.CreateTable(
            name: "SshActions",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                TargetId = table.Column<Guid>(nullable: false),
                Name = table.Column<string>(maxLength: 200, nullable: false),
                CommandJson = table.Column<string>(nullable: false),
                CommandTemplateJson = table.Column<string>(nullable: false),
                IsMutating = table.Column<bool>(nullable: false),
                Risk = table.Column<int>(nullable: false),
                TimeoutSeconds = table.Column<int>(nullable: true),
                OutputLimitBytes = table.Column<int>(nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SshActions", x => x.Id);
                table.ForeignKey(
                    name: "FK_SshActions_SshTargets_TargetId",
                    column: x => x.TargetId,
                    principalTable: "SshTargets",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateTable(
            name: "SshProfiles",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                TargetId = table.Column<Guid>(nullable: false),
                Name = table.Column<string>(maxLength: 200, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SshProfiles", x => x.Id);
                table.ForeignKey(
                    name: "FK_SshProfiles_SshTargets_TargetId",
                    column: x => x.TargetId,
                    principalTable: "SshTargets",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateTable(
            name: "SshActionAllowedParameters",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                ActionId = table.Column<Guid>(nullable: false),
                Name = table.Column<string>(maxLength: 200, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SshActionAllowedParameters", x => x.Id);
                table.ForeignKey(
                    name: "FK_SshActionAllowedParameters_SshActions_ActionId",
                    column: x => x.ActionId,
                    principalTable: "SshActions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateTable(
            name: "SshProfileActions",
            columns: table => new
            {
                ProfileId = table.Column<Guid>(nullable: false),
                ActionId = table.Column<Guid>(nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SshProfileActions", x => new { x.ProfileId, x.ActionId });
                table.ForeignKey(
                    name: "FK_SshProfileActions_SshActions_ActionId",
                    column: x => x.ActionId,
                    principalTable: "SshActions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
                table.ForeignKey(
                    name: "FK_SshProfileActions_SshProfiles_ProfileId",
                    column: x => x.ProfileId,
                    principalTable: "SshProfiles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateTable(
            name: "SshActionAllowedParameterValues",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                AllowedParameterId = table.Column<Guid>(nullable: false),
                Value = table.Column<string>(maxLength: 500, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SshActionAllowedParameterValues", x => x.Id);
                table.ForeignKey(
                    name: "FK_SshActionAllowedParameterValues_SshActionAllowedParameters_AllowedParameterId",
                    column: x => x.AllowedParameterId,
                    principalTable: "SshActionAllowedParameters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateIndex(
            name: "IX_SshActionAllowedParameters_ActionId_Name",
            table: "SshActionAllowedParameters",
            columns: new[] { "ActionId", "Name" },
            unique: true
        );

        migrationBuilder.CreateIndex(
            name: "IX_SshActionAllowedParameterValues_AllowedParameterId_Value",
            table: "SshActionAllowedParameterValues",
            columns: new[] { "AllowedParameterId", "Value" },
            unique: true
        );

        migrationBuilder.CreateIndex(
            name: "IX_SshActions_TargetId_Name",
            table: "SshActions",
            columns: new[] { "TargetId", "Name" },
            unique: true
        );

        migrationBuilder.CreateIndex(
            name: "IX_SshProfileActions_ActionId",
            table: "SshProfileActions",
            column: "ActionId"
        );

        migrationBuilder.CreateIndex(
            name: "IX_SshProfiles_TargetId_Name",
            table: "SshProfiles",
            columns: new[] { "TargetId", "Name" },
            unique: true
        );

        migrationBuilder.CreateIndex(
            name: "IX_SshTargets_Alias",
            table: "SshTargets",
            column: "Alias",
            unique: true
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "SshActionAllowedParameterValues");
        migrationBuilder.DropTable(name: "SshProfileActions");
        migrationBuilder.DropTable(name: "SshActionAllowedParameters");
        migrationBuilder.DropTable(name: "SshProfiles");
        migrationBuilder.DropTable(name: "SshActions");
        migrationBuilder.DropTable(name: "SshTargets");
    }
}
