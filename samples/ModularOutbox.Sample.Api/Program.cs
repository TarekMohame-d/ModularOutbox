using Microsoft.EntityFrameworkCore;
using ModularOutbox.Abstractions;
using ModularOutbox.Core.DependencyInjection;
using ModularOutbox.PostgreSQL.DependencyInjection;
using ModularOutbox.PostgreSQL.Interceptors;
using ModularOutbox.Sample.Api.Database;
using ModularOutbox.Sample.Api.Modules.Identity;

var builder = WebApplication.CreateBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString("Database")
    ?? "Host=localhost;Port=5432;Database=modular_outbox_sample;Username=postgres;Password=postgres";

// 1. Configure EF Core DbContext with Outbox Interceptor
builder.Services.AddDbContext<SampleDbContext>(
    (sp, options) =>
    {
        options
            .UseNpgsql(connectionString)
            .AddInterceptors(sp.GetRequiredService<OutboxSaveChangesInterceptor>());
    }
);

// 2. Configure ModularOutbox Infrastructure
builder.Services.AddModularOutbox(outbox =>
{
    outbox.ConfigureOptions(options =>
    {
        options.BatchSize = 100;
        options.PollingIntervalSeconds = 10;
        options.MaxRetries = 3;
    });

    // Storage Engine and DbContext Bindings
    outbox.UsePostgreSqlStorage(connectionString);
    outbox.RegisterModuleDbContext<SampleDbContext>();

    // Register Handlers from current assembly & add Polly Resilience
    outbox.RegisterHandlersFromAssemblies(typeof(Program).Assembly).EnableResilienceDecorator();
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
        IOutboxWriter<SampleDbContext> outboxWriter,
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
        outboxWriter.Write(@event);

        // Atomic Save: User + Outbox Message committed together
        await dbContext.SaveChangesAsync(ct);

        return Results.Created($"/api/users/{user.Id}", new { user.Id, user.Email });
    }
);

app.Run();

public record RegisterUserRequest(string Email);
