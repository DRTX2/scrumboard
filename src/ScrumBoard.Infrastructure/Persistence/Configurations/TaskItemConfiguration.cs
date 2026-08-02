using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScrumBoard.Domain.Boards;
using ScrumBoard.Domain.Projects;
using ScrumBoard.Domain.Tasks;
using ScrumBoard.Domain.Users;

namespace ScrumBoard.Infrastructure.Persistence.Configurations;

internal sealed class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable("tasks");
        builder.HasKey(task => task.Id);
        builder.Property(task => task.Id).HasColumnName("id");
        builder.Property(task => task.ProjectId).HasColumnName("project_id");
        builder.Property(task => task.ColumnId).HasColumnName("column_id");
        builder.Property(task => task.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(task => task.Description).HasColumnName("description").HasMaxLength(4_000);
        builder.Property(task => task.Priority).HasColumnName("priority").HasConversion<string>().HasMaxLength(16);
        builder.Property(task => task.AssigneeId).HasColumnName("assignee_id");
        builder.Property(task => task.DueDate).HasColumnName("due_date");
        builder.Property(task => task.Position).HasColumnName("position");
        builder.Property(task => task.Version).HasColumnName("version").IsConcurrencyToken();
        builder.Property(task => task.CreatedAt).HasColumnName("created_at");
        builder.Property(task => task.UpdatedAt).HasColumnName("updated_at");
        builder.HasOne<Project>().WithMany().HasForeignKey(task => task.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<BoardColumn>().WithMany().HasForeignKey(task => task.ColumnId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(task => task.AssigneeId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(task => new { task.ColumnId, task.Position })
            .HasDatabaseName("ix_tasks_column_position");
        builder.HasIndex(task => new { task.ProjectId, task.AssigneeId }).HasDatabaseName("ix_tasks_project_assignee");
        builder.HasIndex(task => new { task.ProjectId, task.Priority }).HasDatabaseName("ix_tasks_project_priority");
    }
}
