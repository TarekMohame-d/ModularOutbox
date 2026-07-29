using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ModularOutbox.Core.Models;
using ModularOutbox.Core.Options;

namespace ModularOutbox.PostgreSQL.Persistence;

internal sealed class MessagingDbContext(
    DbContextOptions<MessagingDbContext> options,
    IOptions<ModularOutboxOptions> outboxOptions
) : DbContext(options)
{
    private readonly string _schema = outboxOptions.Value.Schema;

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(_schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MessagingDbContext).Assembly);
    }
}
