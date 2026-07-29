using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ModularOutbox.Abstractions;
using ModularOutbox.Core.Models;

namespace ModularOutbox.PostgreSQL.Persistence;

public sealed class EfOutboxWriter<TContext>(TContext dbContext) : IOutboxWriter<TContext>
    where TContext : DbContext
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public void Write<TEvent>(TEvent integrationEvent)
        where TEvent : class, IIntegrationEvent
    {
        var outboxMessage = new OutboxMessage(
            id: integrationEvent.Id,
            type: integrationEvent.GetType().AssemblyQualifiedName ?? integrationEvent.GetType().FullName!,
            payload: JsonSerializer.Serialize(integrationEvent, SerializerOptions),
            occurredAtUtc: DateTimeOffset.UtcNow
        );

        dbContext.Set<OutboxMessage>().Add(outboxMessage);
    }
}
