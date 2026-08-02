using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScrumBoard.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddTaskDueDates : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateOnly>(
            name: "due_date",
            table: "tasks",
            type: "date",
            nullable: true);

        migrationBuilder.UpdateData(
            table: "tasks",
            keyColumn: "id",
            keyValue: new Guid("40000000-0000-0000-0000-000000000001"),
            column: "due_date",
            value: null);

        migrationBuilder.UpdateData(
            table: "tasks",
            keyColumn: "id",
            keyValue: new Guid("40000000-0000-0000-0000-000000000002"),
            column: "due_date",
            value: null);

        migrationBuilder.UpdateData(
            table: "tasks",
            keyColumn: "id",
            keyValue: new Guid("40000000-0000-0000-0000-000000000003"),
            column: "due_date",
            value: null);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "due_date",
            table: "tasks");
    }
}
