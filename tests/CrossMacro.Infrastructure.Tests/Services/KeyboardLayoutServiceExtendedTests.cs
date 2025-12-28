using CrossMacro.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace CrossMacro.Infrastructure.Tests.Services;

public class KeyboardLayoutServiceExtendedTests
{
    private readonly KeyboardLayoutService _service;

    public KeyboardLayoutServiceExtendedTests()
    {
        _service = new KeyboardLayoutService();
    }

    [Theory]
    [InlineData(59, "F1")]
    [InlineData(68, "F10")]
    [InlineData(87, "F11")]
    [InlineData(88, "F12")]
    [InlineData(183, "F13")]
    [InlineData(194, "F24")]
    public void GetKeyName_ShouldReturnCorrectFKeys(int keyCode, string expectedName)
    {
        _service.GetKeyName(keyCode).Should().Be(expectedName);
        _service.GetKeyCode(expectedName).Should().Be(keyCode);
    }

    [Theory]
    [InlineData(29, "Ctrl")]
    [InlineData(97, "Ctrl")]
    [InlineData(42, "Shift")]
    [InlineData(54, "Shift")]
    [InlineData(56, "Alt")]
    [InlineData(100, "AltGr")]
    [InlineData(125, "Super")]
    public void GetKeyName_ShouldReturnCorrectModifierKeys(int keyCode, string expectedName)
    {
        _service.GetKeyName(keyCode).Should().Be(expectedName);
        // Note: GetKeyCode reverse mapping might map "Ctrl" to 29 (Left Ctrl) by default, 
        // effectively aliasing Right Ctrl to Left Ctrl ID in reverse lookup, which is acceptable behavior.
        // We just verify GetKeyName here primarily.
    }

    [Theory]
    [InlineData(82, "Numpad0")]
    [InlineData(79, "Numpad1")]
    [InlineData(80, "Numpad2")]
    [InlineData(81, "Numpad3")]
    [InlineData(75, "Numpad4")]
    [InlineData(76, "Numpad5")]
    [InlineData(77, "Numpad6")]
    [InlineData(71, "Numpad7")]
    [InlineData(72, "Numpad8")]
    [InlineData(73, "Numpad9")]
    [InlineData(74, "Numpad-")]
    [InlineData(78, "Numpad+")]
    [InlineData(55, "Numpad*")]
    [InlineData(98, "Numpad/")]
    [InlineData(96, "NumpadEnter")]
    [InlineData(83, "Numpad.")]
    [InlineData(117, "Numpad=")]
    public void GetKeyName_ShouldReturnCorrectNumpadKeys(int keyCode, string expectedName)
    {
        _service.GetKeyName(keyCode).Should().Be(expectedName);
        _service.GetKeyCode(expectedName).Should().Be(keyCode);
    }

    [Theory]
    [InlineData(69, "NumLock")]
    [InlineData(70, "ScrollLock")]
    [InlineData(58, "CapsLock")]
    [InlineData(99, "PrintScreen")]
    [InlineData(119, "Pause")]
    public void GetKeyName_ShouldReturnCorrectSpecialKeys(int keyCode, string expectedName)
    {
        _service.GetKeyName(keyCode).Should().Be(expectedName);
        _service.GetKeyCode(expectedName).Should().Be(keyCode);
    }
}
