using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScrumBoard.Adapters.Outbound.Persistence.Migrations;

/// <inheritdoc />
public partial class AddIdempotencyReplayHeaders : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "board_etag",
            table: "idempotency_records",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "etag",
            table: "idempotency_records",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "board_etag",
            table: "idempotency_records");

        migrationBuilder.DropColumn(
            name: "etag",
            table: "idempotency_records");
    }
}
