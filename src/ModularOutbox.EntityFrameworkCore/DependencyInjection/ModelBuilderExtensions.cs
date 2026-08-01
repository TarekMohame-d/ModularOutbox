using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Options;
using ModularOutbox.Core.Options;
using ModularOutbox.EntityFrameworkCore.Configurations;

namespace ModularOutbox.EntityFrameworkCore.DependencyInjection;

/// <summary>
/// Provides Entity Framework Core <see cref="ModelBuilder"/> extension methods
/// for mapping ModularOutbox outbox and inbox table schemas.
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Applies the EF Core entity configurations for ModularOutbox outbox and inbox message tables
    /// to the specified <see cref="ModelBuilder"/> using the configured database schema.
    /// </summary>
    /// <param name="modelBuilder">The <see cref="ModelBuilder"/> being used to construct the database model for the context.</param>
    /// <param name="dbContext">
    /// The current <see cref="DbContext"/> instance, used to resolve <see cref="ModularOutboxOptions"/>
    /// from EF Core's underlying service provider.
    /// </param>
    /// <returns>
    /// The updated <see cref="ModelBuilder"/> instance to allow fluent method chaining.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="modelBuilder"/> or <paramref name="dbContext"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <see cref="ModularOutboxOptions"/> cannot be resolved from the dependency injection container.
    /// </exception>
    /// <remarks>
    /// <para>
    /// This method resolves the configured <see cref="ModularOutboxOptions"/> directly from the <see cref="DbContext"/>'s
    /// internal service infrastructure via <c>dbContext.GetService&lt;IOptions&lt;ModularOutboxOptions&gt;&gt;()</c>.
    /// </para>
    /// <para>
    /// It automatically registers both:
    /// <list type="bullet">
    ///   <item><description><c>OutboxMessageConfiguration</c>: Maps outbox events, payload metadata, retry counters, and lock state.</description></item>
    ///   <item><description><c>InboxMessageConfiguration</c>: Maps consumer idempotency records and processing timestamps.</description></item>
    /// </list>
    /// Both configurations respect the target database schema specified in <see cref="ModularOutboxOptions.Schema"/> (defaults to <c>"messaging"</c>).
    /// </para>
    /// </remarks>
    /// <example>
    /// The following example demonstrates calling <see cref="ApplyModularOutboxConfigurations"/> inside <see cref="DbContext.OnModelCreating"/>:
    /// <code>
    /// public class ApplicationDbContext : DbContext
    /// {
    ///     public ApplicationDbContext(DbContextOptions&lt;ApplicationDbContext&gt; options) : base(options) { }
    ///
    ///     protected override void OnModelCreating(ModelBuilder modelBuilder)
    ///     {
    ///         base.OnModelCreating(modelBuilder);
    ///
    ///         // Apply ModularOutbox entity configurations (Outbox and Inbox tables)
    ///         modelBuilder.ApplyModularOutboxConfigurations(this);
    ///     }
    /// }
    /// </code>
    /// </example>
    public static ModelBuilder ApplyModularOutboxConfigurations(
        this ModelBuilder modelBuilder,
        DbContext dbContext
    )
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentNullException.ThrowIfNull(dbContext);

        var outboxOptions = dbContext.GetService<IOptions<ModularOutboxOptions>>().Value;

        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration(outboxOptions.Schema));
        modelBuilder.ApplyConfiguration(new InboxMessageConfiguration(outboxOptions.Schema));

        return modelBuilder;
    }
}
