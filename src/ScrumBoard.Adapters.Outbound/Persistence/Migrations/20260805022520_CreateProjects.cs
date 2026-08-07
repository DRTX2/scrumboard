using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScrumBoard.Adapters.Outbound.Persistence.Migrations;

/// <inheritdoc />
public partial class CreateProjects : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "projects",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                start_date = table.Column<DateOnly>(type: "date", nullable: false),
                expected_end_date = table.Column<DateOnly>(type: "date", nullable: false),
                status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                version = table.Column<long>(type: "bigint", nullable: false),
                board_version = table.Column<long>(type: "bigint", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_projects", x => x.id);
                table.CheckConstraint("ck_projects_dates", "expected_end_date >= start_date");
            });

        migrationBuilder.CreateTable(
            name: "project_members",
            columns: table => new
            {
                project_id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_project_members", x => new { x.project_id, x.user_id });
                table.ForeignKey(
                    name: "FK_project_members_projects_project_id",
                    column: x => x.project_id,
                    principalTable: "projects",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_project_members_users_user_id",
                    column: x => x.user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_project_members_user_id",
            table: "project_members",
            column: "user_id");

        migrationBuilder.CreateIndex(
            name: "ix_projects_name",
            table: "projects",
            column: "name");

        migrationBuilder.CreateIndex(
            name: "ix_projects_updated_at",
            table: "projects",
            column: "updated_at");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "project_members");

        migrationBuilder.DropTable(
            name: "projects");
    }
}
