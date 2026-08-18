
namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class GenericDisplaySessionServiceTests
{
    [Fact]
    public void IsSessionSupported_ReturnsTrueWithEmptyReason()
    {
        var service = new GenericDisplaySessionService();

        var result = service.IsSessionSupported(out var reason);

        _ = result.Should().BeTrue();
        _ = reason.Should().BeEmpty();
    }
}
