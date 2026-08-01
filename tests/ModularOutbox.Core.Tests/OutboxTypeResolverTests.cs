using ModularOutbox.Abstractions;
using ModularOutbox.Abstractions.Attributes;
using ModularOutbox.Core.Services;
using Shouldly;

namespace ModularOutbox.Core.Tests;

[OutboxMessageName("custom.order-created.v1")]
public record CustomNamedEvent(Guid OrderId) : IntegrationEvent;

public record UnnamedEvent(string Message) : IntegrationEvent;

public class OutboxTypeResolverTests
{
    private readonly OutboxTypeResolver _sut = new();

    [Fact]
    public void GetMessageName_WhenAttributeIsPresent_ReturnsExplicitName()
    {
        // Act
        var name = _sut.GetMessageName(typeof(CustomNamedEvent));

        // Assert
        name.ShouldBe("custom.order-created.v1");
    }

    [Fact]
    public void GetMessageName_WhenAttributeIsMissing_ReturnsAssemblyQualifiedFormat()
    {
        // Act
        var name = _sut.GetMessageName(typeof(UnnamedEvent));

        // Assert
        name.ShouldBe($"{typeof(UnnamedEvent).FullName}, {typeof(UnnamedEvent).Assembly.GetName().Name}");
    }

    [Fact]
    public void ResolveType_WhenExplicitAttributeNameProvided_ResolvesCorrectType()
    {
        // Act
        var resolvedType = _sut.ResolveType("custom.order-created.v1");

        // Assert
        resolvedType.ShouldBe(typeof(CustomNamedEvent));
    }

    [Fact]
    public void ResolveType_WhenFullyQualifiedAssemblyStringProvided_ResolvesCorrectType()
    {
        // Arrange
        var qualifiedName =
            $"{typeof(UnnamedEvent).FullName}, {typeof(UnnamedEvent).Assembly.GetName().Name}";

        // Act
        var resolvedType = _sut.ResolveType(qualifiedName);

        // Assert
        resolvedType.ShouldBe(typeof(UnnamedEvent));
    }

    [Fact]
    public void ResolveType_WhenTypeCannotBeFound_ThrowsInvalidOperationException()
    {
        // Act & Assert
        var ex = Should.Throw<InvalidOperationException>(() =>
            _sut.ResolveType("NonExistent.Namespace.UnknownEvent, NonExistentAssembly")
        );

        ex.Message.ShouldContain("Unable to resolve C# type for stored outbox message");
    }
}
