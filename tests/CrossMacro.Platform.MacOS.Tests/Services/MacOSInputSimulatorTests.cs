using CrossMacro.Platform.MacOS.Services;
using CrossMacro.Platform.MacOS.Native;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Abstractions;
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
    public void CreateKeyboardFlags_DoesNotRequestPostEventAccess()
    {
        var postRequests = 0;
        var simulator = new MacOSInputSimulator(() =>
        {
            postRequests++;
            return true;
        });

        _ = simulator.UpdateKeyboardFlags(InputEventCode.KEY_LEFTMETA, pressed: true);

        Assert.Equal(0, postRequests);
    }

    [Fact]
    public void KeyPress_WhenPostEventPermissionRequestFails_ThrowsPermissionRequired()
    {
        var simulator = new MacOSInputSimulator(() => false, isMacOS: () => true);

        var exception = Assert.Throws<InputInjectionPermissionRequiredException>(
            () => simulator.KeyPress(InputEventCode.KEY_A, pressed: true));

        Assert.Contains("Accessibility", exception.Message);
        Assert.Contains("Input Monitoring", exception.Message);
    }

    [Fact]
    public void KeyPress_WhenPostEventPermissionRequestFails_RechecksOnNextAttempt()
    {
        var postRequests = 0;
        var simulator = new MacOSInputSimulator(
            () =>
            {
                postRequests++;
                return false;
            },
            isMacOS: () => true);

        Assert.Throws<InputInjectionPermissionRequiredException>(
            () => simulator.KeyPress(InputEventCode.KEY_A, pressed: true));
        Assert.Throws<InputInjectionPermissionRequiredException>(
            () => simulator.KeyPress(InputEventCode.KEY_A, pressed: true));

        Assert.Equal(2, postRequests);
    }

    [Fact]
    public void KeyPress_WhenPostEventPermissionEventuallySucceeds_CachesGrantedPermission()
    {
        var postRequests = 0;
        var simulator = new MacOSInputSimulator(
            () =>
            {
                postRequests++;
                return postRequests == 2;
            },
            isMacOS: () => true);

        Assert.Throws<InputInjectionPermissionRequiredException>(
            () => simulator.KeyPress(InputEventCode.KEY_A, pressed: true));
        simulator.KeyPress(InputEventCode.KEY_F21, pressed: true);
        simulator.KeyPress(InputEventCode.KEY_F21, pressed: false);

        Assert.Equal(2, postRequests);
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

    [Theory]
    [InlineData(InputEventCode.KEY_VOLUMEUP, 0)]
    [InlineData(InputEventCode.KEY_VOLUMEDOWN, 1)]
    [InlineData(InputEventCode.KEY_BRIGHTNESSUP, 2)]
    [InlineData(InputEventCode.KEY_BRIGHTNESSDOWN, 3)]
    [InlineData(InputEventCode.KEY_MUTE, 7)]
    [InlineData(InputEventCode.KEY_PLAYPAUSE, 16)]
    [InlineData(InputEventCode.KEY_NEXTSONG, 17)]
    [InlineData(InputEventCode.KEY_PREVIOUSSONG, 18)]
    [InlineData(InputEventCode.KEY_FASTFORWARD, 19)]
    [InlineData(InputEventCode.KEY_REWIND, 20)]
    public void TryGetSystemDefinedKeyType_WhenMacSystemKey_ReturnsNxKeyType(int keyCode, int expectedNxKeyType)
    {
        bool mapped = MacOSInputSimulator.TryGetSystemDefinedKeyType(keyCode, out var nxKeyType);

        Assert.True(mapped);
        Assert.Equal(expectedNxKeyType, nxKeyType);
    }

    [Theory]
    [InlineData(InputEventCode.KEY_F1)]
    [InlineData(InputEventCode.KEY_F12)]
    [InlineData(InputEventCode.KEY_A)]
    public void TryGetSystemDefinedKeyType_WhenOrdinaryKeyboardKey_ReturnsNoMatch(int keyCode)
    {
        bool mapped = MacOSInputSimulator.TryGetSystemDefinedKeyType(keyCode, out _);

        Assert.False(mapped);
    }

    [Theory]
    [InlineData(0, true, 0x000A00)]
    [InlineData(0, false, 0x000B00)]
    [InlineData(2, true, 0x020A00)]
    [InlineData(3, false, 0x030B00)]
    [InlineData(16, true, 0x100A00)]
    public void CreateSystemDefinedData1_EncodesNxKeyTypeAndPressState(int nxKeyType, bool pressed, long expectedData1)
    {
        var payload = MacOSSystemKeyEventFactory.CreatePayload(nxKeyType, pressed);

        Assert.Equal(expectedData1, payload.Data1);
    }

    [Fact]
    public void CreateSystemDefinedData1_WhenPressed_UsesGoldenPressEncoding()
    {
        var payload = MacOSSystemKeyEventFactory.CreatePayload(19, pressed: true);

        Assert.Equal((19 << 16) | 0x0A00, payload.Data1);
    }

    [Fact]
    public void CreateSystemDefinedData1_WhenReleased_UsesGoldenReleaseEncoding()
    {
        var payload = MacOSSystemKeyEventFactory.CreatePayload(20, pressed: false);

        Assert.Equal((20 << 16) | 0x0B00, payload.Data1);
    }

    [Fact]
    public void CreateSystemDefinedEventFlags_WhenPressed_IncludesNxKeyDownStateAndActiveModifiers()
    {
        var payload = MacOSSystemKeyEventFactory.CreatePayload(
            0,
            pressed: true,
            CoreGraphics.CGEventFlags.Command | CoreGraphics.CGEventFlags.Shift);

        Assert.True(payload.Flags.HasFlag(CoreGraphics.CGEventFlags.Command));
        Assert.True(payload.Flags.HasFlag(CoreGraphics.CGEventFlags.Shift));
        Assert.True(((ulong)payload.Flags & 0x0A00) == 0x0A00);
    }

    [Fact]
    public void CreateSystemDefinedEventFlags_WhenReleased_IncludesNxKeyUpState()
    {
        var payload = MacOSSystemKeyEventFactory.CreatePayload(0, pressed: false);

        Assert.True(((ulong)payload.Flags & 0x0B00) == 0x0B00);
    }

    [Fact]
    public void CreateSystemDefinedPayload_UsesGoldenSystemDefinedFields()
    {
        var payload = MacOSSystemKeyEventFactory.CreatePayload(
            16,
            pressed: true,
            CoreGraphics.CGEventFlags.Command);

        Assert.Equal(CoreGraphics.CGEventType.SystemDefined, payload.EventType);
        Assert.True(payload.Flags.HasFlag(CoreGraphics.CGEventFlags.Command));
        Assert.True(((ulong)payload.Flags & 0x0A00) == 0x0A00);
        Assert.Equal(8, payload.Subtype);
        Assert.Equal((16 << 16) | 0x0A00, payload.Data1);
        Assert.Equal(-1, payload.Data2);
    }

    [Fact]
    public void SystemKeyFactory_ExposesNSEventBridgeAvailabilityWithoutRequiringMacOSRuntime()
    {
        var bridgeAvailable = MacOSSystemKeyEventFactory.IsNSEventBridgeAvailable;

        if (!OperatingSystem.IsMacOS())
        {
            Assert.False(bridgeAvailable);
        }
    }

    [Fact]
    public void SystemKeyFactory_IncludesNSEventBridgeImplementation()
    {
        var bridgeType = typeof(MacOSSystemKeyEventFactory).Assembly.GetType(
            "CrossMacro.Platform.MacOS.Services.MacOSSystemKeyNSEventBridge");
        var createMethod = bridgeType?.GetMethod(
            "TryCreateSystemDefinedCGEvent",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(bridgeType);
        Assert.NotNull(createMethod);
    }

    [Fact]
    public void ResolveKeyboardEventRoute_WhenOrdinaryKeyboardKey_ReturnsKeyboardRoute()
    {
        var route = MacOSInputSimulator.ResolveKeyboardEventRoute(
            InputEventCode.KEY_A,
            out var nxKeyType,
            out var virtualKeyCode);

        Assert.Equal(MacOSKeyboardEventRoute.Keyboard, route);
        Assert.Equal(-1, nxKeyType);
        Assert.Equal(0x00, virtualKeyCode);
    }

    [Fact]
    public void ResolveKeyboardEventRoute_WhenSystemKeyAlsoHasVirtualKey_PrefersSystemDefinedRoute()
    {
        var route = MacOSInputSimulator.ResolveKeyboardEventRoute(
            InputEventCode.KEY_VOLUMEUP,
            out var nxKeyType,
            out var virtualKeyCode);

        Assert.Equal(MacOSKeyboardEventRoute.SystemDefined, route);
        Assert.Equal(0, nxKeyType);
        Assert.Equal(0xFFFF, virtualKeyCode);
    }

    [Fact]
    public void ResolveKeyboardEventRoute_WhenKeyboardKeyIsUnsupported_ReturnsUnsupportedRoute()
    {
        var route = MacOSInputSimulator.ResolveKeyboardEventRoute(
            InputEventCode.KEY_F21,
            out var nxKeyType,
            out var virtualKeyCode);

        Assert.Equal(MacOSKeyboardEventRoute.Unsupported, route);
        Assert.Equal(-1, nxKeyType);
        Assert.Equal(0xFFFF, virtualKeyCode);
    }
}
