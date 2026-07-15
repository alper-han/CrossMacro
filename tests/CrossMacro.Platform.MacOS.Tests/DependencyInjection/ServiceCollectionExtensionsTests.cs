
namespace CrossMacro.Platform.MacOS.Tests.DependencyInjection;

[SupportedOSPlatform("macos")]
public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddMacOSServices_RegistersExpectedServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMacOSServices();
        using var provider = services.BuildServiceProvider();

        // Assert
        Assert.IsType<MacOSInputCapture>(provider.GetRequiredService<IInputCapture>());
        Assert.IsType<MacOSInputSimulator>(provider.GetRequiredService<IInputSimulator>());
        Assert.IsType<MacOSMousePositionProvider>(provider.GetRequiredService<IMousePositionProvider>());
        Assert.IsType<MacOSPermissionCheckerService>(provider.GetRequiredService<IPermissionChecker>());
    }
}
