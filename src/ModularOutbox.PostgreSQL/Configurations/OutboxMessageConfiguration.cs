using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModularOutbox.Core.Models;

namespace ModularOutbox.PostgreSQL.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("event_id");
        builder.Property(x => x.Type).HasColumnName("type").IsRequired();
        builder.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.OccurredAtUtc).HasColumnName("occurred_at_utc").IsRequired();
        builder.Property(x => x.ProcessedAtUtc).HasColumnName("processed_at_utc");
        builder.Property(x => x.Error).HasColumnName("error");
        builder.Property(x => x.RetryCount).HasColumnName("retry_count").IsRequired();
        builder.Property(x => x.DeadLetter).HasColumnName("dead_letter").IsRequired();

        builder
            .HasIndex(x => x.OccurredAtUtc)
            .HasDatabaseName("idx_outbox_unprocessed")
            .HasFilter("processed_at_utc IS NULL AND NOT dead_letter");
    }
}
