using Microsoft.EntityFrameworkCore;
using ModularOutbox.PostgreSQL.Configurations;

namespace ModularOutbox.PostgreSQL.DependencyInjection;

public static class ModelBuilderExtensions
{
    public static ModelBuilder UseOutboxModel(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new InboxMessageConfiguration());

        return modelBuilder;
    }
}
