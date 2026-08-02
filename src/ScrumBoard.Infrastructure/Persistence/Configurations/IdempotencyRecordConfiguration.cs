using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScrumBoard.Domain.Idempotency;

namespace ScrumBoard.Infrastructure.Persistence.Configurations;

internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("idempotency_records");
        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id).HasColumnName("id");
        builder.Property(record => record.UserId).HasColumnName("user_id");
        builder.Property(record => record.Operation).HasColumnName("operation").HasMaxLength(160);
        builder.Property(record => record.Key).HasColumnName("key").HasMaxLength(100);
        builder.Property(record => record.RequestHash).HasColumnName("request_hash").HasMaxLength(64);
        builder.Property(record => record.StatusCode).HasColumnName("status_code");
        builder.Property(record => record.ContentType).HasColumnName("content_type").HasMaxLength(100);
        builder.Property(record => record.ResponseBody).HasColumnName("response_body").HasColumnType("jsonb");
        builder.Property(record => record.Location).HasColumnName("location").HasMaxLength(500);
        builder.Property(record => record.CreatedAt).HasColumnName("created_at");
        builder.Property(record => record.ExpiresAt).HasColumnName("expires_at");
        builder.Property(record => record.CompletedAt).HasColumnName("completed_at");
        builder.Ignore(record => record.IsCompleted);
        builder.HasIndex(record => new { record.UserId, record.Operation, record.Key })
            .IsUnique().HasDatabaseName("ux_idempotency_user_operation_key");
        builder.HasIndex(record => record.ExpiresAt).HasDatabaseName("ix_idempotency_expires_at");
    }
}
