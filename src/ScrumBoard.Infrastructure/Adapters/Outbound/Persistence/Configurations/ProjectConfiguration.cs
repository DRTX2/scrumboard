using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScrumBoard.Domain.Projects;

namespace ScrumBoard.Infrastructure.Adapters.Outbound.Persistence.Configurations;

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects");
        builder.HasKey(project => project.Id);
        builder.Property(project => project.Id).HasColumnName("id");
        builder.Property(project => project.Name).HasColumnName("name").HasMaxLength(160).IsRequired();
        builder.Property(project => project.Description).HasColumnName("description").HasMaxLength(2_000);
        builder.Property(project => project.StartDate).HasColumnName("start_date");
        builder.Property(project => project.ExpectedEndDate).HasColumnName("expected_end_date");
        builder.Property(project => project.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(24);
        builder.Property(project => project.Version).HasColumnName("version").IsConcurrencyToken();
        builder.Property(project => project.BoardVersion).HasColumnName("board_version").IsConcurrencyToken();
        builder.Property(project => project.CreatedAt).HasColumnName("created_at");
        builder.Property(project => project.UpdatedAt).HasColumnName("updated_at");
        builder.HasMany(project => project.Members)
            .WithOne()
            .HasForeignKey(member => member.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(project => project.Members).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(project => project.Name).HasDatabaseName("ix_projects_name");
        builder.HasIndex(project => project.UpdatedAt).HasDatabaseName("ix_projects_updated_at");
        builder.ToTable(table => table.HasCheckConstraint(
            "ck_projects_dates",
            "expected_end_date >= start_date"));
    }
}
