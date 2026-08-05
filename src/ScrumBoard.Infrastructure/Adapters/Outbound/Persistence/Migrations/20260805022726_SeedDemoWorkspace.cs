using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ScrumBoard.Infrastructure.Adapters.Outbound.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedDemoWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "projects",
                columns: new[] { "id", "board_version", "created_at", "description", "expected_end_date", "name", "start_date", "status", "updated_at", "version" },
                values: new object[] { new Guid("20000000-0000-0000-0000-000000000001"), 1L, new DateTimeOffset(new DateTime(2026, 7, 30, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Shared project used to demonstrate real-time collaboration.", new DateOnly(2026, 8, 30), "ScrumBoard Launch", new DateOnly(2026, 7, 30), "Active", new DateTimeOffset(new DateTime(2026, 7, 30, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1L });

            migrationBuilder.InsertData(
                table: "board_columns",
                columns: new[] { "id", "created_at", "name", "position", "project_id", "updated_at", "version" },
                values: new object[,]
                {
                    { new Guid("30000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 7, 30, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Backlog", 1024L, new Guid("20000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 7, 30, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1L },
                    { new Guid("30000000-0000-0000-0000-000000000002"), new DateTimeOffset(new DateTime(2026, 7, 30, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "In progress", 2048L, new Guid("20000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 7, 30, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1L },
                    { new Guid("30000000-0000-0000-0000-000000000003"), new DateTimeOffset(new DateTime(2026, 7, 30, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Done", 3072L, new Guid("20000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 7, 30, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1L }
                });

            migrationBuilder.InsertData(
                table: "project_members",
                columns: new[] { "project_id", "user_id", "role" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000001"), new Guid("10000000-0000-0000-0000-000000000001"), "Owner" },
                    { new Guid("20000000-0000-0000-0000-000000000001"), new Guid("10000000-0000-0000-0000-000000000002"), "Member" }
                });

            migrationBuilder.InsertData(
                table: "tasks",
                columns: new[] { "id", "assignee_id", "column_id", "created_at", "description", "due_date", "position", "priority", "project_id", "title", "updated_at", "version" },
                values: new object[,]
                {
                    { new Guid("40000000-0000-0000-0000-000000000001"), new Guid("10000000-0000-0000-0000-000000000001"), new Guid("30000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 7, 30, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Prioritize the first sprint with the product owner.", null, 1024L, "High", new Guid("20000000-0000-0000-0000-000000000001"), "Review product backlog", new DateTimeOffset(new DateTime(2026, 7, 30, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1L },
                    { new Guid("40000000-0000-0000-0000-000000000002"), new Guid("10000000-0000-0000-0000-000000000002"), new Guid("30000000-0000-0000-0000-000000000002"), new DateTimeOffset(new DateTime(2026, 7, 30, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Implement authenticated real-time updates.", null, 1024L, "Critical", new Guid("20000000-0000-0000-0000-000000000001"), "Build collaborative board", new DateTimeOffset(new DateTime(2026, 7, 30, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1L },
                    { new Guid("40000000-0000-0000-0000-000000000003"), new Guid("10000000-0000-0000-0000-000000000001"), new Guid("30000000-0000-0000-0000-000000000003"), new DateTimeOffset(new DateTime(2026, 7, 30, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Document ports, adapters and trade-offs.", null, 1024L, "Medium", new Guid("20000000-0000-0000-0000-000000000001"), "Define architecture", new DateTimeOffset(new DateTime(2026, 7, 30, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1L }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "project_members",
                keyColumns: new[] { "project_id", "user_id" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000001"), new Guid("10000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                table: "project_members",
                keyColumns: new[] { "project_id", "user_id" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000001"), new Guid("10000000-0000-0000-0000-000000000002") });

            migrationBuilder.DeleteData(
                table: "tasks",
                keyColumn: "id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "tasks",
                keyColumn: "id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "tasks",
                keyColumn: "id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "board_columns",
                keyColumn: "id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "board_columns",
                keyColumn: "id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "board_columns",
                keyColumn: "id",
                keyValue: new Guid("30000000-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "projects",
                keyColumn: "id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000001"));
        }
    }
}
