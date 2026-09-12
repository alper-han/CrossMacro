
namespace CrossMacro.Platform.Linux.Tests.Services;

public sealed class GsrCompatibilityServiceTests
{
    [Fact]
    public void Constructor_WhenFileExistsDelegateIsNull_Throws()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new GsrCompatibilityService(null!, _ => string.Empty));
    }

    [Fact]
    public void Constructor_WhenReadAllTextDelegateIsNull_Throws()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new GsrCompatibilityService(_ => true, null!));
    }

    [Fact]
    public void IsGsrVirtualKeyboardActive_WhenVirtualKeyboardExists_ReturnsTrue()
    {
        var service = new GsrCompatibilityService(
            path => string.Equals(path, LinuxGsrCompatibility.InputDevicesPath, StringComparison.Ordinal),
            _ => "N: Name=\"gsr-ui virtual keyboard\"\nH: Handlers=sysrq kbd event25\n");

        var result = service.IsGsrVirtualKeyboardActive();

        Assert.True(result);
    }

    [Fact]
    public void IsGsrVirtualKeyboardActive_WhenInputDevicesFileIsMissing_ReturnsFalse()
    {
        var service = new GsrCompatibilityService(
            _ => false,
            _ => throw new InvalidOperationException("Should not read missing file."));

        var result = service.IsGsrVirtualKeyboardActive();

        Assert.False(result);
    }

    [Fact]
    public void IsGsrVirtualKeyboardActive_WhenReadFails_ReturnsFalse()
    {
        var service = new GsrCompatibilityService(
            _ => true,
            _ => throw new UnauthorizedAccessException());

        var result = service.IsGsrVirtualKeyboardActive();

        Assert.False(result);
    }

    [Fact]
    public void GetStartupNotification_WhenGsrVirtualKeyboardIsActive_ReturnsWarningDetails()
    {
        var service = new GsrCompatibilityService(
            _ => true,
            _ => "N: Name=\"gsr-ui virtual keyboard\"\nH: Handlers=sysrq kbd event25\n");

        var notification = service.GetStartupNotification();

        var result = Assert.IsType<PlatformStartupNotification>(notification);
        Assert.Equal("GPU Screen Recorder", result.Title);
        Assert.Equal(PlatformStartupNotificationSeverity.Warning, result.Severity);
        Assert.Contains("GSR is active", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetStartupNotification_WhenGsrVirtualKeyboardIsInactive_ReturnsNull()
    {
        var service = new GsrCompatibilityService(_ => true, _ => "N: Name=\"other keyboard\"\nH: Handlers=kbd event3\n");

        var notification = service.GetStartupNotification();

        Assert.Null(notification);
    }

    [Fact]
    public void ContainsGsrVirtualKeyboard_WhenContentDoesNotContainGsrDevice_ReturnsFalse()
    {
        var result = LinuxGsrCompatibility.ContainsGsrVirtualKeyboard(
            "N: Name=\"AT Translated Set 2 keyboard\"\nH: Handlers=sysrq kbd event3\n");

        Assert.False(result);
    }

    [Fact]
    public void ContainsGsrVirtualKeyboard_WhenNameAppearsOutsideDeviceName_ReturnsFalse()
    {
        var result = LinuxGsrCompatibility.ContainsGsrVirtualKeyboard(
            "N: Name=\"AT Translated Set 2 keyboard\"\nH: Handlers=sysrq kbd event3\nP: Phys=gsr-ui virtual keyboard\n");

        Assert.False(result);
    }

    [Fact]
    public void ContainsGsrVirtualKeyboard_WhenMatchingNameLacksKeyboardHandler_ReturnsFalse()
    {
        var result = LinuxGsrCompatibility.ContainsGsrVirtualKeyboard(
            "N: Name=\"gsr-ui virtual keyboard\"\nH: Handlers=event25\n");

        Assert.False(result);
    }

    [Fact]
    public void ContainsGsrVirtualKeyboard_WhenMatchingNameLacksEventHandler_ReturnsFalse()
    {
        var result = LinuxGsrCompatibility.ContainsGsrVirtualKeyboard(
            "N: Name=\"gsr-ui virtual keyboard\"\nH: Handlers=sysrq kbd\n");

        Assert.False(result);
    }
}
