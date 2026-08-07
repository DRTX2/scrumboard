using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScrumBoard.Adapters.Outbound.Persistence.Migrations;

/// <inheritdoc />
public partial class CreateBoard : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "board_columns",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                project_id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                position = table.Column<long>(type: "bigint", nullable: false),
                version = table.Column<long>(type: "bigint", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_board_columns", x => x.id);
                table.ForeignKey(
                    name: "FK_board_columns_projects_project_id",
                    column: x => x.project_id,
                    principalTable: "projects",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "tasks",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                project_id = table.Column<Guid>(type: "uuid", nullable: false),
                column_id = table.Column<Guid>(type: "uuid", nullable: false),
                title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                priority = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                assignee_id = table.Column<Guid>(type: "uuid", nullable: true),
                due_date = table.Column<DateOnly>(type: "date", nullable: true),
                position = table.Column<long>(type: "bigint", nullable: false),
                version = table.Column<long>(type: "bigint", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tasks", x => x.id);
                table.ForeignKey(
                    name: "FK_tasks_board_columns_column_id",
                    column: x => x.column_id,
                    principalTable: "board_columns",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_tasks_projects_project_id",
                    column: x => x.project_id,
                    principalTable: "projects",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_tasks_users_assignee_id",
                    column: x => x.assignee_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "ix_board_columns_project_position",
            table: "board_columns",
            columns: new[] { "project_id", "position" });

        migrationBuilder.CreateIndex(
            name: "IX_tasks_assignee_id",
            table: "tasks",
            column: "assignee_id");

        migrationBuilder.CreateIndex(
            name: "ix_tasks_column_position",
            table: "tasks",
            columns: new[] { "column_id", "position" });

        migrationBuilder.CreateIndex(
            name: "ix_tasks_project_assignee",
            table: "tasks",
            columns: new[] { "project_id", "assignee_id" });

        migrationBuilder.CreateIndex(
            name: "ix_tasks_project_priority",
            table: "tasks",
            columns: new[] { "project_id", "priority" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "tasks");

        migrationBuilder.DropTable(
            name: "board_columns");
    }
}
