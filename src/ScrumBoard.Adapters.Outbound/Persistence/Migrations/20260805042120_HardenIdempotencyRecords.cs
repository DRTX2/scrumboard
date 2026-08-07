using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScrumBoard.Adapters.Outbound.Persistence.Migrations;

/// <inheritdoc />
public partial class HardenIdempotencyRecords : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ux_idempotency_user_operation_key",
            table: "idempotency_records");

        migrationBuilder.Sql(
            "ALTER TABLE idempotency_records ALTER COLUMN response_body TYPE text USING response_body::text;");

        migrationBuilder.CreateIndex(
            name: "ux_idempotency_user_key",
            table: "idempotency_records",
            columns: new[] { "user_id", "key" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ux_idempotency_user_key",
            table: "idempotency_records");

        migrationBuilder.Sql(
            "ALTER TABLE idempotency_records ALTER COLUMN response_body TYPE jsonb USING response_body::jsonb;");

        migrationBuilder.CreateIndex(
            name: "ux_idempotency_user_operation_key",
            table: "idempotency_records",
            columns: new[] { "user_id", "operation", "key" },
            unique: true);
    }
}
