using ModularOutbox.Abstractions;

namespace ModularOutbox.Sample.Api.Modules.Identity;

public sealed record UserRegisteredIntegrationEvent(Guid UserId, string Email) : IntegrationEvent;
