using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScrumBoard.Domain.Boards;
using ScrumBoard.Domain.Projects;

namespace ScrumBoard.Adapters.Outbound.Persistence.Configurations;

internal sealed class BoardColumnConfiguration : IEntityTypeConfiguration<BoardColumn>
{
    public void Configure(EntityTypeBuilder<BoardColumn> builder)
    {
        builder.ToTable("board_columns", table =>
        {
            table.HasCheckConstraint("ck_board_columns_position", "position > 0");
            table.HasCheckConstraint("ck_board_columns_version", "version > 0");
        });
        builder.HasKey(column => column.Id);
        builder.HasAlternateKey(column => new { column.ProjectId, column.Id })
            .HasName("AK_board_columns_project_id_id");
        builder.Property(column => column.Id).HasColumnName("id");
        builder.Property(column => column.ProjectId).HasColumnName("project_id");
        builder.Property(column => column.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(column => column.Position).HasColumnName("position");
        builder.Property(column => column.Version).HasColumnName("version").IsConcurrencyToken();
        builder.Property(column => column.CreatedAt).HasColumnName("created_at");
        builder.Property(column => column.UpdatedAt).HasColumnName("updated_at");
        builder.HasOne<Project>().WithMany().HasForeignKey(column => column.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(column => new { column.ProjectId, column.Position, column.Id })
            .HasDatabaseName("ix_board_columns_project_position");
    }
}
