using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModularOutbox.Abstractions;
using ModularOutbox.Core.DependencyInjection;
using ModularOutbox.EntityFrameworkCore.DependencyInjection;
using ModularOutbox.PostgreSQL.DependencyInjection;
using ModularOutbox.PostgreSQL.Tests.Integration.Fixture;
using Shouldly;

namespace ModularOutbox.PostgreSQL.Tests.Integration;

public record OrderPlacedIntegrationEvent(Guid OrderId, decimal Amount) : IntegrationEvent;

public class OrderPlacedEventHandler(IInboxStore inboxStore)
    : IIntegrationEventHandler<OrderPlacedIntegrationEvent>
{
    public static bool HasProcessed { get; private set; }

    public async Task HandleAsync(OrderPlacedIntegrationEvent @event, CancellationToken ct = default)
    {
        const string consumerName = nameof(OrderPlacedEventHandler);

        if (await inboxStore.HasBeenProcessedAsync(@event.Id, consumerName, ct))
            return;

        // Perform Business Action
        HasProcessed = true;

        await inboxStore.MarkAsProcessedAsync(@event.Id, consumerName, ct);
    }
}

public class IntegrationTestDbContext(DbContextOptions<IntegrationTestDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyModularOutboxConfigurations(this);
    }
}

[Collection(PostgreSqlTestCollection.Name)]
public class EndToEndOutboxIntegrationTests(PostgreSqlTestFixture fixture)
{
    [Fact]
    public async Task EnqueueEvent_CommitsAtomicallyWithEFCore_AndDeliveryServiceDispatchesToHandler()
    {
        // 1. Build Service Provider
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddDbContext<IntegrationTestDbContext>(
            (sp, opts) =>
            {
                opts.UseNpgsql(fixture.ConnectionString).UseModularOutbox(sp);
            }
        );

        services.AddModularOutbox(outbox =>
        {
            outbox.ConfigureOptions(options =>
            {
                options.ConnectionString = fixture.ConnectionString;
                options.Schema = "messaging";
                options.PollingInterval = TimeSpan.FromSeconds(1);
                options.BatchSize = 10;
            });

            outbox
                .RegisterHandlersFromAssemblies(typeof(EndToEndOutboxIntegrationTests).Assembly)
                .UseEntityFrameworkCore()
                .UsePostgreSql(fixture.ConnectionString);
        });

        var provider = services.BuildServiceProvider();

        // 2. Start Hosted Background Services (Delivery & Migrations)
        var hostedServices = provider.GetServices<IHostedService>();
        foreach (var service in hostedServices)
        {
            await service.StartAsync(TestContext.Current.CancellationToken);
        }

        try
        {
            // 3. Stage & Commit Outbox Event in Application Scope
            var orderId = Guid.NewGuid();
            using (var scope = provider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<IntegrationTestDbContext>();
                var outboxWriter = scope.ServiceProvider.GetRequiredService<IOutboxWriter>();

                outboxWriter.Enqueue(
                    new OrderPlacedIntegrationEvent(orderId, 199.99m),
                    TestContext.Current.CancellationToken
                );
                await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // 4. Assert: Wait for background worker to deliver event
            var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (!OrderPlacedEventHandler.HasProcessed && !timeoutCts.IsCancellationRequested)
            {
                await Task.Delay(100, TestContext.Current.CancellationToken);
            }

            OrderPlacedEventHandler.HasProcessed.ShouldBeTrue();
        }
        finally
        {
            foreach (var service in hostedServices)
            {
                await service.StopAsync(CancellationToken.None);
            }
        }
    }
}
