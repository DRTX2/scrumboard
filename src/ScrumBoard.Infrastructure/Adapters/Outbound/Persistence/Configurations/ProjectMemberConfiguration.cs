using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScrumBoard.Domain.Projects;
using ScrumBoard.Domain.Users;

namespace ScrumBoard.Infrastructure.Adapters.Outbound.Persistence.Configurations;

internal sealed class ProjectMemberConfiguration : IEntityTypeConfiguration<ProjectMember>
{
    public void Configure(EntityTypeBuilder<ProjectMember> builder)
    {
        builder.ToTable("project_members", table =>
            table.HasCheckConstraint("ck_project_members_role", "role IN ('Member', 'Owner')"));
        builder.HasKey(member => new { member.ProjectId, member.UserId });
        builder.Property(member => member.ProjectId).HasColumnName("project_id");
        builder.Property(member => member.UserId).HasColumnName("user_id");
        builder.Property(member => member.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(16);
        builder.HasOne<User>().WithMany().HasForeignKey(member => member.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(member => new { member.UserId, member.ProjectId })
            .HasDatabaseName("ix_project_members_user_id");
    }
}
