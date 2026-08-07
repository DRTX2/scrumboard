using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ScrumBoard.Adapters.Outbound.Persistence.Migrations;

/// <inheritdoc />
public partial class SeedDemoUsers : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.InsertData(
            table: "users",
            columns: new[] { "id", "created_at", "email", "is_active", "name", "password_hash" },
            values: new object[,]
            {
                { new Guid("10000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 7, 30, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "owner@scrumboard.local", true, "Demo Owner", "pbkdf2-sha512.210000.EAECAwQFBgcICQoLDA0ODw==./lanLqoVjc6fLDiztMJR6F8AdOXAQlpTUuHreEVVtlk=" },
                { new Guid("10000000-0000-0000-0000-000000000002"), new DateTimeOffset(new DateTime(2026, 7, 30, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "member@scrumboard.local", true, "Demo Member", "pbkdf2-sha512.210000.IAECAwQFBgcICQoLDA0ODw==.sXzKpx2ClnU/GVOq9jn9613AU7KMaVn8zJXl5eGLGSw=" }
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DeleteData(
            table: "users",
            keyColumn: "id",
            keyValue: new Guid("10000000-0000-0000-0000-000000000001"));

        migrationBuilder.DeleteData(
            table: "users",
            keyColumn: "id",
            keyValue: new Guid("10000000-0000-0000-0000-000000000002"));
    }
}
