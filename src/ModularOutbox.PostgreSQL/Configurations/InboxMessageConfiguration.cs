using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModularOutbox.Core.Models;

namespace ModularOutbox.PostgreSQL.Configurations;

public sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("inbox_messages");

        builder.HasKey(x => new { x.Id, x.ConsumerName });

        builder.Property(x => x.Id).HasColumnName("event_id");
        builder.Property(x => x.ConsumerName).HasColumnName("consumer_name").IsRequired();
        builder.Property(x => x.ProcessedAtUtc).HasColumnName("processed_at_utc").IsRequired();
    }
}
