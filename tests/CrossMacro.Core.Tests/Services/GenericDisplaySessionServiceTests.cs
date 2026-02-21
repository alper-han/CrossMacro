using CrossMacro.Core.Services;
using FluentAssertions;

namespace CrossMacro.Core.Tests.Services;

public class GenericDisplaySessionServiceTests
{
    [Fact]
    public void IsSessionSupported_ReturnsTrueWithEmptyReason()
    {
        // Arrange
        var service = new GenericDisplaySessionService();

        // Act
        var result = service.IsSessionSupported(out var reason);

        // Assert
        result.Should().BeTrue();
        reason.Should().BeEmpty();
    }
}
