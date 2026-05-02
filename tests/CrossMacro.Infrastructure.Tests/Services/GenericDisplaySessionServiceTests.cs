using CrossMacro.Infrastructure.Services;
using FluentAssertions;

namespace CrossMacro.Infrastructure.Tests.Services;

public class GenericDisplaySessionServiceTests
{
    [Fact]
    public void IsSessionSupported_ReturnsTrueWithEmptyReason()
    {
        var service = new GenericDisplaySessionService();

        var result = service.IsSessionSupported(out var reason);

        result.Should().BeTrue();
        reason.Should().BeEmpty();
    }
}
