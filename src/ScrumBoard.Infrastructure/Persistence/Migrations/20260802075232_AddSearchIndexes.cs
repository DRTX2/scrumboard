using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScrumBoard.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
            migrationBuilder.Sql("CREATE INDEX ix_projects_name_trgm ON projects USING gin (name gin_trgm_ops);");
            migrationBuilder.Sql("CREATE INDEX ix_tasks_title_trgm ON tasks USING gin (title gin_trgm_ops);");
            migrationBuilder.Sql("CREATE INDEX ix_tasks_description_trgm ON tasks USING gin (description gin_trgm_ops);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_tasks_description_trgm;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_tasks_title_trgm;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_projects_name_trgm;");
        }
    }
}
