using System;
using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Abstractions.Diagnostics;
using CrossMacro.Platform.Linux.Services;

namespace CrossMacro.Platform.Linux.Tests.Services;

public sealed class GsrCompatibilityServiceTests
{
    [Fact]
    public void IsGsrVirtualKeyboardActive_WhenVirtualKeyboardExists_ReturnsTrue()
    {
        var service = new GsrCompatibilityService(
            path => path == LinuxGsrCompatibility.InputDevicesPath,
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
