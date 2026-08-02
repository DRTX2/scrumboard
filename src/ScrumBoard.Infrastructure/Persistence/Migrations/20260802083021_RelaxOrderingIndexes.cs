using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScrumBoard.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class RelaxOrderingIndexes : Migration
{
    private static readonly string[] TaskPositionColumns = ["column_id", "position"];
    private static readonly string[] ColumnPositionColumns = ["project_id", "position"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ux_tasks_column_position",
            table: "tasks");

        migrationBuilder.DropIndex(
            name: "ux_board_columns_project_position",
            table: "board_columns");

        migrationBuilder.CreateIndex(
            name: "ix_tasks_column_position",
            table: "tasks",
            columns: TaskPositionColumns);

        migrationBuilder.CreateIndex(
            name: "ix_board_columns_project_position",
            table: "board_columns",
            columns: ColumnPositionColumns);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_tasks_column_position",
            table: "tasks");

        migrationBuilder.DropIndex(
            name: "ix_board_columns_project_position",
            table: "board_columns");

        migrationBuilder.CreateIndex(
            name: "ux_tasks_column_position",
            table: "tasks",
            columns: TaskPositionColumns,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ux_board_columns_project_position",
            table: "board_columns",
            columns: ColumnPositionColumns,
            unique: true);
    }
}
