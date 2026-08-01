using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModularOutbox.Core.Models;
using ModularOutbox.Core.Options;

namespace ModularOutbox.EntityFrameworkCore.Configurations;

internal sealed class OutboxMessageConfiguration(string Schema) : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages", Schema);

        // Primary Key
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        // Column Mappings
        builder.Property(x => x.MessageId).HasColumnName("message_id");

        builder.Property(x => x.MessageType).HasColumnName("message_type");

        builder.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb");

        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamptz");

        builder
            .Property(x => x.ProcessedAtUtc)
            .HasColumnName("processed_at_utc")
            .HasColumnType("timestamptz");

        builder
            .Property(x => x.LockedUntilUtc)
            .HasColumnName("locked_until_utc")
            .HasColumnType("timestamptz");

        builder.Property(x => x.RetryCount).HasColumnName("retry_count");

        builder.Property(x => x.LastError).HasColumnName("last_error");
    }
}
