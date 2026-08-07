using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScrumBoard.Adapters.Outbound.Persistence.Migrations;

/// <inheritdoc />
public partial class RequireTaskAssigneeAndAddChecks : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_tasks_board_columns_column_id",
            table: "tasks");

        migrationBuilder.DropForeignKey(
            name: "FK_tasks_users_assignee_id",
            table: "tasks");

        migrationBuilder.DropIndex(
            name: "IX_tasks_assignee_id",
            table: "tasks");

        migrationBuilder.DropIndex(
            name: "ix_tasks_column_position",
            table: "tasks");

        migrationBuilder.DropIndex(
            name: "ix_project_members_user_id",
            table: "project_members");

        migrationBuilder.DropIndex(
            name: "ix_board_columns_project_position",
            table: "board_columns");

        migrationBuilder.Sql(
            """
            UPDATE tasks AS task
            SET assignee_id = (
                SELECT owner.user_id
                FROM project_members AS owner
                WHERE owner.project_id = task.project_id
                  AND owner.role = 'Owner'
                ORDER BY owner.user_id
                LIMIT 1)
            WHERE task.assignee_id IS NULL
               OR NOT EXISTS (
                   SELECT 1
                   FROM project_members AS membership
                   WHERE membership.project_id = task.project_id
                     AND membership.user_id = task.assignee_id);

            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM tasks AS task
                    WHERE task.assignee_id IS NULL
                       OR NOT EXISTS (
                           SELECT 1
                           FROM project_members AS membership
                           WHERE membership.project_id = task.project_id
                             AND membership.user_id = task.assignee_id)) THEN
                    RAISE EXCEPTION 'Cannot repair task assignees because a project has no owner.';
                END IF;
            END $$;
            """);

        migrationBuilder.AlterColumn<Guid>(
            name: "assignee_id",
            table: "tasks",
            type: "uuid",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true);

        migrationBuilder.AddUniqueConstraint(
            name: "AK_board_columns_project_id_id",
            table: "board_columns",
            columns: new[] { "project_id", "id" });

        migrationBuilder.CreateIndex(
            name: "ix_tasks_column_position",
            table: "tasks",
            columns: new[] { "column_id", "position", "id" });

        migrationBuilder.CreateIndex(
            name: "IX_tasks_project_id_column_id",
            table: "tasks",
            columns: new[] { "project_id", "column_id" });

        migrationBuilder.AddCheckConstraint(
            name: "ck_tasks_position",
            table: "tasks",
            sql: "position > 0");

        migrationBuilder.AddCheckConstraint(
            name: "ck_tasks_priority",
            table: "tasks",
            sql: "priority IN ('Low', 'Medium', 'High', 'Critical')");

        migrationBuilder.AddCheckConstraint(
            name: "ck_tasks_version",
            table: "tasks",
            sql: "version > 0");

        migrationBuilder.AddCheckConstraint(
            name: "ck_projects_status",
            table: "projects",
            sql: "status IN ('Planned', 'Active', 'Completed', 'Archived')");

        migrationBuilder.AddCheckConstraint(
            name: "ck_projects_versions",
            table: "projects",
            sql: "version > 0 AND board_version > 0");

        migrationBuilder.CreateIndex(
            name: "ix_project_members_user_id",
            table: "project_members",
            columns: new[] { "user_id", "project_id" });

        migrationBuilder.AddCheckConstraint(
            name: "ck_project_members_role",
            table: "project_members",
            sql: "role IN ('Member', 'Owner')");

        migrationBuilder.CreateIndex(
            name: "ix_board_columns_project_position",
            table: "board_columns",
            columns: new[] { "project_id", "position", "id" });

        migrationBuilder.AddCheckConstraint(
            name: "ck_board_columns_position",
            table: "board_columns",
            sql: "position > 0");

        migrationBuilder.AddCheckConstraint(
            name: "ck_board_columns_version",
            table: "board_columns",
            sql: "version > 0");

        migrationBuilder.AddForeignKey(
            name: "FK_tasks_board_columns_project_id_column_id",
            table: "tasks",
            columns: new[] { "project_id", "column_id" },
            principalTable: "board_columns",
            principalColumns: new[] { "project_id", "id" },
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_tasks_project_members_project_id_assignee_id",
            table: "tasks",
            columns: new[] { "project_id", "assignee_id" },
            principalTable: "project_members",
            principalColumns: new[] { "project_id", "user_id" },
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_tasks_board_columns_project_id_column_id",
            table: "tasks");

        migrationBuilder.DropForeignKey(
            name: "FK_tasks_project_members_project_id_assignee_id",
            table: "tasks");

        migrationBuilder.DropIndex(
            name: "ix_tasks_column_position",
            table: "tasks");

        migrationBuilder.DropIndex(
            name: "IX_tasks_project_id_column_id",
            table: "tasks");

        migrationBuilder.DropCheckConstraint(
            name: "ck_tasks_position",
            table: "tasks");

        migrationBuilder.DropCheckConstraint(
            name: "ck_tasks_priority",
            table: "tasks");

        migrationBuilder.DropCheckConstraint(
            name: "ck_tasks_version",
            table: "tasks");

        migrationBuilder.DropCheckConstraint(
            name: "ck_projects_status",
            table: "projects");

        migrationBuilder.DropCheckConstraint(
            name: "ck_projects_versions",
            table: "projects");

        migrationBuilder.DropIndex(
            name: "ix_project_members_user_id",
            table: "project_members");

        migrationBuilder.DropCheckConstraint(
            name: "ck_project_members_role",
            table: "project_members");

        migrationBuilder.DropUniqueConstraint(
            name: "AK_board_columns_project_id_id",
            table: "board_columns");

        migrationBuilder.DropIndex(
            name: "ix_board_columns_project_position",
            table: "board_columns");

        migrationBuilder.DropCheckConstraint(
            name: "ck_board_columns_position",
            table: "board_columns");

        migrationBuilder.DropCheckConstraint(
            name: "ck_board_columns_version",
            table: "board_columns");

        migrationBuilder.AlterColumn<Guid>(
            name: "assignee_id",
            table: "tasks",
            type: "uuid",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uuid");

        migrationBuilder.CreateIndex(
            name: "IX_tasks_assignee_id",
            table: "tasks",
            column: "assignee_id");

        migrationBuilder.CreateIndex(
            name: "ix_tasks_column_position",
            table: "tasks",
            columns: new[] { "column_id", "position" });

        migrationBuilder.CreateIndex(
            name: "ix_project_members_user_id",
            table: "project_members",
            column: "user_id");

        migrationBuilder.CreateIndex(
            name: "ix_board_columns_project_position",
            table: "board_columns",
            columns: new[] { "project_id", "position" });

        migrationBuilder.AddForeignKey(
            name: "FK_tasks_board_columns_column_id",
            table: "tasks",
            column: "column_id",
            principalTable: "board_columns",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_tasks_users_assignee_id",
            table: "tasks",
            column: "assignee_id",
            principalTable: "users",
            principalColumn: "id",
            onDelete: ReferentialAction.SetNull);
    }
}
