using Microsoft.EntityFrameworkCore;
using ModularOutbox.EntityFrameworkCore.DependencyInjection;
using ModularOutbox.PostgreSQL.DependencyInjection;

namespace ModularOutbox.Sample.Api.Database;

public sealed class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = default!;
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class SampleDbContext(DbContextOptions<SampleDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Map Sample User Entity
        modelBuilder.Entity<User>(builder =>
        {
            builder.ToTable("users", "identity");
            builder.HasKey(u => u.Id);
            builder.Property(u => u.Email).IsRequired();
        });

        // Apply ModularOutbox Entity Configurations for PostgreSQL
        modelBuilder.ApplyModularOutboxConfigurations(this);
    }
}
