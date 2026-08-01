# ModularOutbox

> A lightweight, production-ready **Transactional Outbox + Inbox** implementation for .NET applications using Entity Framework Core and PostgreSQL.

ModularOutbox helps you reliably publish integration events without the dual-write problem by storing events in the same database transaction as your business data.

## Features

- ✅ Transactional Outbox Pattern
- ✅ Inbox Pattern for idempotent consumers
- ✅ Entity Framework Core integration
- ✅ PostgreSQL storage provider
- ✅ Automatic integration event discovery
- ✅ Automatic background delivery service
- ✅ Automatic cleanup service
- ✅ Polly resilience support
- ✅ Configurable retry policies
- ✅ UUIDv7 event identifiers
- ✅ High-performance batch processing
- ✅ Safe for multiple application instances
- ✅ Custom JSON serialization options

---

# Architecture

```text
                    HTTP Request
                          │
                          ▼
                  Application Service
                          │
          ┌───────────────┴────────────────┐
          │                                │
          ▼                                ▼
     Save Business Data            Enqueue Integration Event
          │                                │
          └───────────────┬────────────────┘
                          ▼
                 DbContext.SaveChanges()
                          │
                Single Database Transaction
                          │
          ┌───────────────┴────────────────┐
          │                                │
          ▼                                ▼
      Business Tables                 Outbox Table
                                              │
                                              ▼
                                  Background Delivery Service
                                              │
                                              ▼
                                   Integration Event Dispatcher
                                              │
                                              ▼
                                    Registered Event Handlers
                                              │
                                              ▼
                                         Inbox Table
```

---

# Installation

Install the required packages.

```bash
dotnet add package ModularOutbox.Core
dotnet add package ModularOutbox.EntityFrameworkCore
dotnet add package ModularOutbox.PostgreSQL
```

---

# Quick Start

## 1. Configure Entity Framework Core

```csharp
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    options
        .UseNpgsql(builder.Configuration.GetConnectionString("Database"))
        .UseModularOutbox(sp);
});
```

---

## 2. Register ModularOutbox

```csharp
builder.Services.AddModularOutbox(outbox =>
{
    outbox.ConfigureOptions(options =>
    {
        options.ConnectionString =
            builder.Configuration.GetConnectionString("Database")!;

        options.Schema = "messaging";
        options.BatchSize = 100;

        options.EnableDeliveryService = true;
        options.EnableCleanupService = true;
    });

    outbox
        .RegisterHandlersFromAssemblies(typeof(Program).Assembly)
        .AddResilienceDecorators()
        .UseEntityFrameworkCore()
        .UsePostgreSql(
            builder.Configuration.GetConnectionString("Database")!);
});
```

---

## 3. Configure your DbContext

Apply the ModularOutbox entity configurations.

```csharp
public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyModularOutboxConfigurations(this);
    }
}
```

---

## 4. Create an Integration Event

Simply inherit from `IntegrationEvent`.

```csharp
public sealed record UserRegisteredIntegrationEvent(
    Guid UserId,
    string Email
) : IntegrationEvent;
```

### Optional: Stable Message Names

To avoid breaking message contracts after refactoring, assign a logical message name.

```csharp
[OutboxMessageName("identity.user-registered.v1")]
public sealed record UserRegisteredIntegrationEvent(
    Guid UserId,
    string Email
) : IntegrationEvent;
```

---

## 5. Publish an Event

Inject `IOutboxWriter` and enqueue the event before calling `SaveChangesAsync()`.

```csharp
public sealed class RegisterUserHandler
{
    public async Task Handle(
        RegisterUser command,
        AppDbContext dbContext,
        IOutboxWriter outboxWriter,
        CancellationToken ct)
    {
        var user = new User(command.Email);

        dbContext.Users.Add(user);

        outboxWriter.Enqueue(
            new UserRegisteredIntegrationEvent(
                user.Id,
                user.Email));

        await dbContext.SaveChangesAsync(ct);
    }
}
```

> **Important**
>
> `SaveChangesAsync()` commits both your entity changes and the outbox message in the **same database transaction**.
>
> If the transaction fails, neither the entity nor the event is persisted.

---

## 6. Handle Events

Implement `IIntegrationEventHandler<T>`.

```csharp
public sealed class UserRegisteredHandler(
    IInboxStore inboxStore,
    ILogger<UserRegisteredHandler> logger)
    : IIntegrationEventHandler<UserRegisteredIntegrationEvent>
{
    private const string Consumer =
        nameof(UserRegisteredHandler);

    public async Task HandleAsync(
        UserRegisteredIntegrationEvent integrationEvent,
        CancellationToken ct)
    {
        if (await inboxStore.HasBeenProcessedAsync(
            integrationEvent.Id,
            Consumer,
            ct))
        {
            return;
        }

        logger.LogInformation(
            "Sending welcome email to {Email}",
            integrationEvent.Email);

        await inboxStore.MarkAsProcessedAsync(
            integrationEvent.Id,
            Consumer,
            ct);
    }
}
```

The `IInboxStore` ensures your handlers are **idempotent**, preventing duplicate processing when messages are retried.

---

# Resilience

ModularOutbox integrates with **Microsoft.Extensions.Resilience / Polly**.

Register a resilience pipeline.

```csharp
builder.Services.AddResiliencePipeline(
    "emails",
    pipeline =>
    {
        pipeline.AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3
        });
    });
```

Apply it to your handler.

```csharp
[ResilientHandler("emails")]
public sealed class UserRegisteredHandler
    : IIntegrationEventHandler<UserRegisteredIntegrationEvent>
{
}
```

If no attribute is specified, the default pipeline registered by `AddResilienceDecorators()` is used.

---

# Configuration

```csharp
builder.Services.AddModularOutbox(outbox =>
{
    outbox.ConfigureOptions(options =>
    {
        options.ConnectionString = "...";
        options.Schema = "messaging";
        options.BatchSize = 100;
        options.PollingInterval = TimeSpan.FromSeconds(10);
        options.LockTimeout = TimeSpan.FromSeconds(15);
        options.MaxRetries = 5;
        options.CleanupAfter = TimeSpan.FromHours(24);
        options.CleanupInterval = TimeSpan.FromHours(12);

        options.EnableDeliveryService = true;
        options.EnableCleanupService = true;
    });
});
```

| Option | Default | Description |
|---------|---------|-------------|
| `ConnectionString` | Required | PostgreSQL connection string |
| `Schema` | `messaging` | Database schema |
| `BatchSize` | `100` | Number of messages processed per batch |
| `PollingInterval` | `10 seconds` | Idle polling interval |
| `LockTimeout` | `15 seconds` | Message lease duration |
| `MaxRetries` | `5` | Maximum retry attempts |
| `CleanupAfter` | `24 hours` | Message retention period |
| `CleanupInterval` | `12 hours` | Cleanup execution interval |
| `EnableDeliveryService` | `true` | Enables background delivery service |
| `EnableCleanupService` | `true` | Enables cleanup service |

---

# How It Works

1. Your application modifies business data.
2. Integration events are staged using `IOutboxWriter`.
3. `SaveChangesAsync()` persists both business data and outbox messages in a single transaction.
4. The background delivery service fetches pending messages.
5. Messages are deserialized and dispatched.
6. Registered handlers execute.
7. `IInboxStore` records successful processing to guarantee idempotency.
8. Successfully processed messages are cleaned up automatically.

---

# Sample

```csharp
app.MapPost(
    "/users",
    async (
        CreateUserRequest request,
        AppDbContext db,
        IOutboxWriter outbox,
        CancellationToken ct) =>
    {
        var user = new User(request.Email);

        db.Users.Add(user);

        outbox.Enqueue(
            new UserRegisteredIntegrationEvent(
                user.Id,
                user.Email));

        await db.SaveChangesAsync(ct);

        return Results.Ok();
    });
```

---

# Roadmap

- [x] Transactional Outbox
- [x] Inbox Pattern
- [x] PostgreSQL provider
- [x] Entity Framework Core integration
- [x] Polly resilience support
- [ ] SQL Server provider
- [ ] MongoDB provider
- [ ] Distributed transport integrations
- [ ] Metrics and OpenTelemetry

---

# License

Licensed under the MIT License.
