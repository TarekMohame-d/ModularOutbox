using Microsoft.EntityFrameworkCore;
using ModularOutbox.Abstractions;
using ModularOutbox.Core.DependencyInjection;
using ModularOutbox.EntityFrameworkCore.DependencyInjection;
using ModularOutbox.PostgreSQL.DependencyInjection;
using ModularOutbox.Sample.Api.Database;
using ModularOutbox.Sample.Api.Modules.Identity;
using Polly;
using Polly.Retry;

var builder = WebApplication.CreateBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString("Database")
    ?? "Host=localhost;Port=5432;Database=modular_outbox_sample;Username=postgres;Password=postgres";

// 1. Configure EF Core DbContext with Outbox Interceptor
builder.Services.AddDbContext<SampleDbContext>(
    (sp, options) =>
    {
        options.UseNpgsql(connectionString).UseModularOutbox(sp);
    }
);

builder.Services.AddResiliencePipeline("test", pipelineBuilder =>
{
    pipelineBuilder.AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 2,
        Delay = TimeSpan.FromMilliseconds(50),
        BackoffType = DelayBackoffType.Constant
    });
});

// 2. Configure ModularOutbox Infrastructure
builder.Services.AddModularOutbox(outbox =>
{
    outbox.ConfigureOptions(options =>
    {
        options.BatchSize = 20;
        options.Schema = "messaging";
        options.PollingInterval = TimeSpan.FromSeconds(10);
        options.EnableDeliveryService = true;
        options.EnableCleanupService = true;
        options.MaxRetries = 3;
    });

    outbox
        .RegisterHandlersFromAssemblies(typeof(Program).Assembly)
        .AddResilienceDecorators()
        .UseEntityFrameworkCore()
        .UsePostgreSql(connectionString);
});

var app = builder.Build();

// Automatically create database schema on startup for quick testing
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
}

// Minimal API Endpoint: Register User
app.MapPost(
    "/api/users/register",
    async (
        RegisterUserRequest request,
        SampleDbContext dbContext,
        IOutboxWriter outboxWriter,
        CancellationToken ct
    ) =>
    {
        var user = new User
        {
            Id = Guid.CreateVersion7(),
            Email = request.Email,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        // Stage User Entity
        dbContext.Users.Add(user);

        // Stage Integration Event to Outbox
        var @event = new UserRegisteredIntegrationEvent(user.Id, user.Email);
        outboxWriter.Enqueue(@event);

        // Atomic Save: User + Outbox Message committed together
        await dbContext.SaveChangesAsync(ct);

        return Results.Created($"/api/users/{user.Id}", new { user.Id, user.Email });
    }
);

app.Run();

public record RegisterUserRequest(string Email);
