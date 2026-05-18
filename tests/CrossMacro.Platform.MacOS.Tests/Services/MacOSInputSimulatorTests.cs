using CrossMacro.Platform.MacOS.Services;
using CrossMacro.Platform.MacOS.Native;
using CrossMacro.Core.Services;
using Xunit;

namespace CrossMacro.Platform.MacOS.Tests.Services;

public class MacOSInputSimulatorTests
{
    [Fact]
    public void ProviderName_IsExpected()
    {
        var simulator = new MacOSInputSimulator();

        Assert.Equal("macOS CoreGraphics", simulator.ProviderName);
    }

    [Fact]
    public void IsSupported_MatchesCurrentPlatform()
    {
        var simulator = new MacOSInputSimulator();

        Assert.Equal(OperatingSystem.IsMacOS(), simulator.IsSupported);
    }

    [Fact]
    public void SupportsUnicodeTextInput_MatchesPlatformSupport()
    {
        var simulator = new MacOSInputSimulator();

        Assert.IsAssignableFrom<IUnicodeTextInputSimulator>(simulator);
        Assert.IsAssignableFrom<ITaggedKeyboardInputSimulator>(simulator);
        Assert.IsAssignableFrom<ITaggedUnicodeTextInputSimulator>(simulator);
        Assert.Equal(simulator.IsSupported, simulator.SupportsUnicodeTextInput);
        Assert.Equal(simulator.IsSupported, simulator.SupportsTaggedKeyboardInput);
    }

    [Fact]
    public void UsesMetaKeyForStandardPaste_IsEnabled()
    {
        var simulator = new MacOSInputSimulator();

        Assert.IsAssignableFrom<IPlatformPasteShortcutProvider>(simulator);
        Assert.True(simulator.UsesMetaKeyForStandardPaste);
    }

    [Fact]
    public void CreateKeyboardFlags_WhenMetaIsPressed_IncludesCommandFlag()
    {
        var flags = MacOSInputSimulator.CreateKeyboardFlags([InputEventCode.KEY_LEFTMETA]);

        Assert.True(flags.HasFlag(CoreGraphics.CGEventFlags.Command));
    }

    [Fact]
    public void CreateKeyboardFlags_WhenCommonModifiersArePressed_IncludesMatchingMacFlags()
    {
        var flags = MacOSInputSimulator.CreateKeyboardFlags(
            [
                InputEventCode.KEY_LEFTCTRL,
                InputEventCode.KEY_LEFTSHIFT,
                InputEventCode.KEY_LEFTALT
            ]);

        Assert.True(flags.HasFlag(CoreGraphics.CGEventFlags.Control));
        Assert.True(flags.HasFlag(CoreGraphics.CGEventFlags.Shift));
        Assert.True(flags.HasFlag(CoreGraphics.CGEventFlags.Alternate));
        Assert.False(flags.HasFlag(CoreGraphics.CGEventFlags.Command));
    }

    [Fact]
    public void UpdateKeyboardFlags_WhenMetaWrapsV_KeepsCommandOnVEventsAndClearsOnRelease()
    {
        var simulator = new MacOSInputSimulator();

        var metaDown = simulator.UpdateKeyboardFlags(InputEventCode.KEY_LEFTMETA, pressed: true);
        var vDown = simulator.UpdateKeyboardFlags(InputEventCode.KEY_V, pressed: true);
        var vUp = simulator.UpdateKeyboardFlags(InputEventCode.KEY_V, pressed: false);
        var metaUp = simulator.UpdateKeyboardFlags(InputEventCode.KEY_LEFTMETA, pressed: false);

        Assert.True(metaDown.HasFlag(CoreGraphics.CGEventFlags.Command));
        Assert.True(vDown.HasFlag(CoreGraphics.CGEventFlags.Command));
        Assert.True(vUp.HasFlag(CoreGraphics.CGEventFlags.Command));
        Assert.False(metaUp.HasFlag(CoreGraphics.CGEventFlags.Command));
    }

    [Fact]
    public void UpdateKeyboardFlags_WhenBothShiftKeysArePressed_ReleasingOneKeepsShiftFlag()
    {
        var simulator = new MacOSInputSimulator();

        simulator.UpdateKeyboardFlags(InputEventCode.KEY_LEFTSHIFT, pressed: true);
        simulator.UpdateKeyboardFlags(InputEventCode.KEY_RIGHTSHIFT, pressed: true);
        var leftShiftUp = simulator.UpdateKeyboardFlags(InputEventCode.KEY_LEFTSHIFT, pressed: false);
        var rightShiftUp = simulator.UpdateKeyboardFlags(InputEventCode.KEY_RIGHTSHIFT, pressed: false);

        Assert.True(leftShiftUp.HasFlag(CoreGraphics.CGEventFlags.Shift));
        Assert.False(rightShiftUp.HasFlag(CoreGraphics.CGEventFlags.Shift));
    }

    [Fact]
    public void UpdateKeyboardFlags_WhenNonModifierIsPressed_DoesNotChangeModifierFlags()
    {
        var simulator = new MacOSInputSimulator();

        var initial = simulator.UpdateKeyboardFlags(InputEventCode.KEY_V, pressed: true);
        simulator.UpdateKeyboardFlags(InputEventCode.KEY_LEFTCTRL, pressed: true);
        var afterNonModifier = simulator.UpdateKeyboardFlags(InputEventCode.KEY_V, pressed: false);

        Assert.Equal(default, initial);
        Assert.True(afterNonModifier.HasFlag(CoreGraphics.CGEventFlags.Control));
    }
}
