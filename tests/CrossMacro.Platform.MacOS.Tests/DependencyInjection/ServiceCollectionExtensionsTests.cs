
namespace CrossMacro.Platform.MacOS.Tests.DependencyInjection;

[SupportedOSPlatform("macos")]
public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddMacOSServices_RegistersExpectedServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        _ = services.AddMacOSServices();
        using var provider = services.BuildServiceProvider();

        // Assert
        _ = Assert.IsType<MacOSInputCapture>(provider.GetRequiredService<IInputCapture>());
        _ = Assert.IsType<MacOSInputSimulator>(provider.GetRequiredService<IInputSimulator>());
        _ = Assert.IsType<MacOSMousePositionProvider>(provider.GetRequiredService<IMousePositionProvider>());
        _ = Assert.IsType<MacOSPermissionCheckerService>(provider.GetRequiredService<IPermissionChecker>());
    }
}
