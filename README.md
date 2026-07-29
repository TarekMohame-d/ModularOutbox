# ModularOutbox

A lightweight, high-performance **Transactional Outbox & Inbox** library for **ASP.NET Core** and **Entity Framework Core**, designed for **Modular Monoliths** and distributed systems.

ModularOutbox guarantees that domain changes and integration events are committed atomically using the **Transactional Outbox Pattern**, while providing **idempotent event consumption**, **automatic retries**, **dead-letter support**, and **horizontal scalability** through PostgreSQL row locking.

> **Status:** Preview

---

## Features

- ✅ Transactional Outbox Pattern
- ✅ Inbox Pattern for idempotent consumers
- ✅ EF Core SaveChanges interceptor integration
- ✅ PostgreSQL optimized (`FOR UPDATE SKIP LOCKED`)
- ✅ Automatic background processing
- ✅ Immediate wake-up after successful transactions
- ✅ Periodic polling fallback
- ✅ Horizontal scaling across multiple application instances
- ✅ Automatic retry with Polly resilience
- ✅ Dead-letter support
- ✅ Batch processing
- ✅ Assembly scanning for event handlers using Scrutor
- ✅ Minimal setup
- ✅ .NET 10 support

---

# Architecture

```text
┌───────────────────────────┐
│ Application               │
│                           │
│ Save Entity               │
│ Write Integration Event   │
└────────────┬──────────────┘
             │
             ▼
      EF Core Transaction
             │
             ▼
┌───────────────────────────┐
│ Commit                    │
│                           │
│ Entity                    │
│ Outbox Message            │
└────────────┬──────────────┘
             │
             ▼
 SaveChangesInterceptor
             │
             ▼
 Signal Background Worker
             │
             ▼
 Fetch Batch (SKIP LOCKED)
             │
             ▼
 Deserialize Event
             │
             ▼
 Dispatch Handlers
             │
             ▼
 Mark Processed
```

---

# Packages

The solution is split into three packages.

| Package                        | Purpose                                                     |
| ------------------------------ | ----------------------------------------------------------- |
| **ModularOutbox.Abstractions** | Interfaces and base event types                             |
| **ModularOutbox.Core**         | Background processor, dispatcher, decorators, configuration |
| **ModularOutbox.PostgreSQL**   | EF Core integration and PostgreSQL implementation           |

---

# Installation

```bash
dotnet add package ModularOutbox.Core
dotnet add package ModularOutbox.PostgreSQL
```

---

# Quick Start

## 1. Configure EF Core

```csharp
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    options
        .UseNpgsql(connectionString)
        .AddInterceptors(
            sp.GetRequiredService<OutboxSaveChangesInterceptor>());
});
```

---

## 2. Register ModularOutbox

```csharp
builder.Services.AddModularOutbox(outbox =>
{
    outbox.ConfigureOptions(options =>
    {
        options.BatchSize = 100;
        options.PollingIntervalSeconds = 10;
        options.MaxRetries = 3;
    });

    outbox.UsePostgreSqlStorage(connectionString);

    outbox.RegisterModuleDbContext<AppDbContext>();

    outbox.RegisterHandlersFromAssemblies(typeof(Program).Assembly)
          .EnableResilienceDecorator();
});
```

---

## 3. Add Outbox Tables

Inside your DbContext:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.UseOutboxModel();

    base.OnModelCreating(modelBuilder);
}
```

---

## 4. Create an Integration Event

```csharp
public sealed record UserRegisteredIntegrationEvent(
    Guid UserId,
    string Email
) : IntegrationEvent;
```

---

## 5. Write Events

```csharp
public sealed class RegisterUserHandler
{
    public async Task Handle(
        RegisterUser command,
        AppDbContext db,
        IOutboxWriter<AppDbContext> outbox)
    {
        var user = new User(...);

        db.Users.Add(user);

        outbox.Write(
            new UserRegisteredIntegrationEvent(
                user.Id,
                user.Email));

        await db.SaveChangesAsync();
    }
}
```

Both the entity and the integration event are committed in the **same transaction**.

---

## 6. Consume Events

```csharp
public sealed class UserRegisteredHandler
    : IIntegrationEventHandler<UserRegisteredIntegrationEvent>
{
    private const string Consumer =
        nameof(UserRegisteredHandler);

    public async Task HandleAsync(
        UserRegisteredIntegrationEvent integrationEvent,
        CancellationToken ct)
    {
        // Business logic...
    }
}
```

---

# Inbox Pattern

To make consumers idempotent:

```csharp
public sealed class UserRegisteredHandler(
    IInboxStore<AppDbContext> inbox)
    : IIntegrationEventHandler<UserRegisteredIntegrationEvent>
{
    private const string Consumer =
        nameof(UserRegisteredHandler);

    public async Task HandleAsync(
        UserRegisteredIntegrationEvent integrationEvent,
        CancellationToken ct)
    {
        if (await inbox.HasBeenProcessedAsync(
                integrationEvent.Id,
                Consumer,
                ct))
        {
            return;
        }

        // Execute business logic

        await inbox.MarkAsProcessedAsync(
            integrationEvent.Id,
            Consumer,
            ct);
    }
}
```

---

# How Processing Works

Whenever `SaveChanges()` commits successfully:

1. The Outbox message is stored in the same transaction.
2. The interceptor signals the background processor.
3. The processor immediately wakes up.
4. A batch of messages is fetched.
5. Events are deserialized.
6. Matching handlers are executed.
7. Successfully processed messages are marked as processed.

If the wake-up signal is missed (for example after an application restart), the background service periodically polls the database to ensure no messages remain unprocessed.

---

# Horizontal Scaling

Multiple application instances can safely process the same Outbox table.

Each worker executes:

```sql
SELECT *
FROM messaging.outbox_messages
FOR UPDATE SKIP LOCKED
LIMIT @BatchSize;
```

This guarantees:

- No duplicate processing
- No distributed locks
- No leader election
- Excellent scalability

Simply deploy multiple replicas behind a load balancer.

---

# Retry & Dead Letter

If a handler throws an exception:

- Retry count increases
- Polly retries the handler
- Errors are stored
- Messages exceeding `MaxRetries` become Dead Letter messages

Dead Letter messages remain in the Outbox table for inspection.

---

# Configuration

```csharp
outbox.ConfigureOptions(options =>
{
    options.BatchSize = 100;

    options.PollingIntervalSeconds = 10;

    options.MaxRetries = 3;

    options.Schema = "messaging";
});
```

| Option                 | Default   | Description                          |
| ---------------------- | --------- | ------------------------------------ |
| BatchSize              | 50        | Number of events processed per batch |
| PollingIntervalSeconds | 10        | Fallback polling interval            |
| MaxRetries             | 3         | Maximum processing attempts          |
| Schema                 | messaging | Database schema                      |

---

# Project Structure

```text
src/
 ├── ModularOutbox.Abstractions
 ├── ModularOutbox.Core
 └── ModularOutbox.PostgreSQL

samples/
 └── ModularOutbox.Sample.Api

tests/
 ├── ModularOutbox.Core.Tests
 └── ModularOutbox.PostgreSQL.Tests
```

---

# Testing

Run all tests:

```bash
dotnet test
```

The PostgreSQL integration tests use:

- Testcontainers
- PostgreSQL
- Respawn

to validate real database behavior, concurrency, and locking.

---

# Requirements

- .NET 10
- Entity Framework Core 10
- PostgreSQL

---

# Roadmap

- [x] Transactional Outbox
- [x] Inbox Pattern
- [x] Background Processor
- [x] Automatic Wake-up Channel
- [x] PostgreSQL `SKIP LOCKED`
- [x] Polly Resilience
- [x] Dead Letter Support
- [x] Batch Processing
- [ ] SQL Server provider
- [ ] MySQL provider
- [ ] Metrics
- [ ] Custom serialization support
- [ ] Distributed tracing

---

# Contributing

Contributions, issues, and feature requests are welcome.

If you find a bug or have an idea for improvement, feel free to open an issue or submit a pull request.

---

# License

This project is licensed under the MIT License.
