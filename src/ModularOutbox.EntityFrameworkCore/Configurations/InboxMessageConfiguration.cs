using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModularOutbox.Core.Models;

namespace ModularOutbox.EntityFrameworkCore.Configurations;

internal sealed class InboxMessageConfiguration(string Schema) : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("inbox_messages", Schema);

        builder.HasKey(x => new { x.MessageId, x.ConsumerName });

        builder.Property(x => x.MessageId).HasColumnName("message_id");
        builder.Property(x => x.ConsumerName).HasColumnName("consumer_name");
        builder
            .Property(x => x.ProcessedAtUtc)
            .HasColumnName("processed_at_utc")
            .HasColumnType("timestamptz");
    }
}
