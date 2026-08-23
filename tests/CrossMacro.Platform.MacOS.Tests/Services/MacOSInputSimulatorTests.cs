
namespace CrossMacro.Platform.MacOS.Tests.Services;

public sealed class MacOSInputSimulatorTests
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

        _ = Assert.IsAssignableFrom<IUnicodeTextInputSimulator>(simulator);
        _ = Assert.IsAssignableFrom<ITaggedKeyboardInputSimulator>(simulator);
        _ = Assert.IsAssignableFrom<ITaggedUnicodeTextInputSimulator>(simulator);
        Assert.Equal(simulator.IsSupported, simulator.SupportsUnicodeTextInput);
        Assert.Equal(simulator.IsSupported, simulator.SupportsTaggedKeyboardInput);
    }

    [Fact]
    public void UsesMetaKeyForStandardPaste_IsEnabled()
    {
        var simulator = new MacOSInputSimulator();

        _ = Assert.IsAssignableFrom<IPlatformPasteShortcutProvider>(simulator);
        Assert.True(simulator.UsesMetaKeyForStandardPaste);
    }

    [Fact]
    public void PostCreatedEvent_WhenCreationFailed_DoesNotPostOrRelease()
    {
        var postCount = 0;
        var releaseCount = 0;

        var posted = MacOSInputSimulator.PostCreatedEvent(
            IntPtr.Zero,
            _ => postCount++,
            _ => releaseCount++);

        Assert.False(posted);
        Assert.Equal(0, postCount);
        Assert.Equal(0, releaseCount);
    }

    [Fact]
    public void PostCreatedEvent_WhenCreationSucceeded_PostsAndAlwaysReleases()
    {
        var releaseCount = 0;

        _ = Assert.Throws<InvalidOperationException>(() => MacOSInputSimulator.PostCreatedEvent(
            new IntPtr(1),
            _ => throw new InvalidOperationException("post failed"),
            _ => releaseCount++));

        Assert.Equal(1, releaseCount);
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

        Assert.Contains("Accessibility", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Input Monitoring", exception.Message, StringComparison.Ordinal);
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

        _ = Assert.Throws<InputInjectionPermissionRequiredException>(
            () => simulator.KeyPress(InputEventCode.KEY_A, pressed: true));
        _ = Assert.Throws<InputInjectionPermissionRequiredException>(
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
                return postRequests is 2;
            },
            isMacOS: () => true);

        _ = Assert.Throws<InputInjectionPermissionRequiredException>(
            () => simulator.KeyPress(InputEventCode.KEY_A, pressed: true));
        simulator.KeyPress(InputEventCode.KEY_F21, pressed: true);
        simulator.KeyPress(InputEventCode.KEY_F21, pressed: false);

        Assert.Equal(2, postRequests);
    }

    [Fact]
    public void CreateKeyboardFlags_WhenMetaIsPressed_IncludesCommandFlag()
    {
        var flags = MacOSInputSimulator.CreateKeyboardFlags([InputEventCode.KEY_LEFTMETA]);

        Assert.True(flags.HasFlag(CoreGraphics.CGEventModifiers.Command));
    }

    [Fact]
    public void CreateKeyboardFlags_WhenCommonModifiersArePressed_IncludesMatchingMacFlags()
    {
        var flags = MacOSInputSimulator.CreateKeyboardFlags(
            [
                InputEventCode.KEY_LEFTCTRL,
                InputEventCode.KEY_LEFTSHIFT,
                InputEventCode.KEY_LEFTALT,
            ]);

        Assert.True(flags.HasFlag(CoreGraphics.CGEventModifiers.Control));
        Assert.True(flags.HasFlag(CoreGraphics.CGEventModifiers.Shift));
        Assert.True(flags.HasFlag(CoreGraphics.CGEventModifiers.Alternate));
        Assert.False(flags.HasFlag(CoreGraphics.CGEventModifiers.Command));
    }

    [Fact]
    public void UpdateKeyboardFlags_WhenMetaWrapsV_KeepsCommandOnVEventsAndClearsOnRelease()
    {
        var simulator = new MacOSInputSimulator();

        var metaDown = simulator.UpdateKeyboardFlags(InputEventCode.KEY_LEFTMETA, pressed: true);
        var vDown = simulator.UpdateKeyboardFlags(InputEventCode.KEY_V, pressed: true);
        var vUp = simulator.UpdateKeyboardFlags(InputEventCode.KEY_V, pressed: false);
        var metaUp = simulator.UpdateKeyboardFlags(InputEventCode.KEY_LEFTMETA, pressed: false);

        Assert.True(metaDown.HasFlag(CoreGraphics.CGEventModifiers.Command));
        Assert.True(vDown.HasFlag(CoreGraphics.CGEventModifiers.Command));
        Assert.True(vUp.HasFlag(CoreGraphics.CGEventModifiers.Command));
        Assert.False(metaUp.HasFlag(CoreGraphics.CGEventModifiers.Command));
    }

    [Fact]
    public void UpdateKeyboardFlags_WhenBothShiftKeysArePressed_ReleasingOneKeepsShiftFlag()
    {
        var simulator = new MacOSInputSimulator();

        _ = simulator.UpdateKeyboardFlags(InputEventCode.KEY_LEFTSHIFT, pressed: true);
        _ = simulator.UpdateKeyboardFlags(InputEventCode.KEY_RIGHTSHIFT, pressed: true);
        var leftShiftUp = simulator.UpdateKeyboardFlags(InputEventCode.KEY_LEFTSHIFT, pressed: false);
        var rightShiftUp = simulator.UpdateKeyboardFlags(InputEventCode.KEY_RIGHTSHIFT, pressed: false);

        Assert.True(leftShiftUp.HasFlag(CoreGraphics.CGEventModifiers.Shift));
        Assert.False(rightShiftUp.HasFlag(CoreGraphics.CGEventModifiers.Shift));
    }

    [Fact]
    public void UpdateKeyboardFlags_WhenNonModifierIsPressed_DoesNotChangeModifierFlags()
    {
        var simulator = new MacOSInputSimulator();

        var initial = simulator.UpdateKeyboardFlags(InputEventCode.KEY_V, pressed: true);
        _ = simulator.UpdateKeyboardFlags(InputEventCode.KEY_LEFTCTRL, pressed: true);
        var afterNonModifier = simulator.UpdateKeyboardFlags(InputEventCode.KEY_V, pressed: false);

        Assert.Equal(default, initial);
        Assert.True(afterNonModifier.HasFlag(CoreGraphics.CGEventModifiers.Control));
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
            CoreGraphics.CGEventModifiers.Command | CoreGraphics.CGEventModifiers.Shift);

        Assert.True(payload.Flags.HasFlag(CoreGraphics.CGEventModifiers.Command));
        Assert.True(payload.Flags.HasFlag(CoreGraphics.CGEventModifiers.Shift));
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
            CoreGraphics.CGEventModifiers.Command);

        Assert.Equal(CoreGraphics.CGEventType.SystemDefined, payload.EventType);
        Assert.True(payload.Flags.HasFlag(CoreGraphics.CGEventModifiers.Command));
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
        TryCreateSystemDefinedCGEventDelegate createEvent = MacOSSystemKeyNSEventBridge.TryCreateSystemDefinedCGEvent;

        Assert.NotNull(createEvent);
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

    [Theory]
    [InlineData(MouseButtonCode.Left, true, (int)CoreGraphics.CGEventType.LeftMouseDown, 0)]
    [InlineData(MouseButtonCode.Right, false, (int)CoreGraphics.CGEventType.RightMouseUp, 1)]
    [InlineData(MouseButtonCode.Middle, true, (int)CoreGraphics.CGEventType.OtherMouseDown, 2)]
    [InlineData(MouseButtonCode.Side1, false, (int)CoreGraphics.CGEventType.OtherMouseUp, 3)]
    [InlineData(MouseButtonCode.Side2, true, (int)CoreGraphics.CGEventType.OtherMouseDown, 4)]
    public void TryResolveMouseButton_ShouldUseCoreGraphicsButtonNumbers(
        int button,
        bool pressed,
        int expectedEventType,
        long expectedButtonNumber)
    {
        var resolved = MacOSInputSimulator.TryResolveMouseButton(
            button,
            pressed,
            out var macButton,
            out var eventType,
            out var buttonNumber);

        Assert.True(resolved);
        Assert.Equal((CoreGraphics.CGEventType)expectedEventType, eventType);
        Assert.Equal(expectedButtonNumber, buttonNumber);
        Assert.Equal((CoreGraphics.CGMouseButton)expectedButtonNumber, macButton);
    }

    [Theory]
    [InlineData(MouseButtonCode.Left, (int)CoreGraphics.CGEventType.LeftMouseDragged, 0)]
    [InlineData(MouseButtonCode.Right, (int)CoreGraphics.CGEventType.RightMouseDragged, 1)]
    [InlineData(MouseButtonCode.Middle, (int)CoreGraphics.CGEventType.OtherMouseDragged, 2)]
    [InlineData(MouseButtonCode.Side1, (int)CoreGraphics.CGEventType.OtherMouseDragged, 3)]
    [InlineData(MouseButtonCode.Side2, (int)CoreGraphics.CGEventType.OtherMouseDragged, 4)]
    public void ResolveMouseMovement_WhenButtonHeld_ShouldEmitMatchingDrag(
        int button,
        int expectedEventType,
        long expectedButtonNumber)
    {
        var movement = MacOSInputSimulator.ResolveMouseMovement(new HashSet<int> { button });

        Assert.Equal((CoreGraphics.CGEventType)expectedEventType, movement.EventType);
        Assert.Equal(expectedButtonNumber, movement.ButtonNumber);
    }

    [Fact]
    public void MoveRelative_WhenEventsArePostedBackToBack_UsesLastPostedTarget()
    {
        var postedPositions = new List<(int X, int Y)>();
        var cursorQueries = 0;
        var simulator = new MacOSInputSimulator(
            requestPostEventAccess: static () => true,
            isMacOS: static () => true,
            getCursorPosition: () =>
            {
                cursorQueries++;
                return new CoreGraphics.CGPoint { X = 100, Y = 80 };
            },
            postMouseMovement: (x, y, _) =>
            {
                postedPositions.Add((x, y));
                return true;
            });
        simulator.Initialize();

        simulator.MoveRelative(10, -5);
        simulator.MoveRelative(4, 7);

        Assert.Equal(1, cursorQueries);
        Assert.Equal([(110, 75), (114, 82)], postedPositions);
    }

    [Fact]
    public void MoveRelative_WhenTargetOverflows_SaturatesPostedPosition()
    {
        (int X, int Y)? postedPosition = null;
        var simulator = new MacOSInputSimulator(
            requestPostEventAccess: static () => true,
            isMacOS: static () => true,
            getCursorPosition: static () => new CoreGraphics.CGPoint
            {
                X = int.MaxValue,
                Y = int.MinValue,
            },
            postMouseMovement: (x, y, _) =>
            {
                postedPosition = (x, y);
                return true;
            });
        simulator.Initialize();

        simulator.MoveRelative(1, -1);

        Assert.Equal((int.MaxValue, int.MinValue), postedPosition);
    }

    [Fact]
    public void MouseButton_AfterPostedMovement_UsesPostedTargetInsteadOfStaleCursorQuery()
    {
        (int X, int Y)? buttonPosition = null;
        var cursorQueries = 0;
        var simulator = new MacOSInputSimulator(
            requestPostEventAccess: static () => true,
            isMacOS: static () => true,
            getCursorPosition: () =>
            {
                cursorQueries++;
                return new CoreGraphics.CGPoint { X = 10, Y = 20 };
            },
            postMouseMovement: static (_, _, _) => true,
            postMouseButton: (_, _, x, y) =>
            {
                buttonPosition = (x, y);
                return true;
            });
        simulator.Initialize();

        simulator.MoveAbsolute(500, 600);
        simulator.MouseButton(MouseButtonCode.Left, pressed: true);

        Assert.Equal(1, cursorQueries);
        Assert.Equal((500, 600), buttonPosition);
    }

    [Fact]
    public void MouseButton_WithoutPostedMovement_UsesLiveCursorPosition()
    {
        (int X, int Y)? buttonPosition = null;
        var cursorPositions = new Queue<CoreGraphics.CGPoint>(
        [
            new CoreGraphics.CGPoint { X = 10, Y = 20 },
            new CoreGraphics.CGPoint { X = 30, Y = 40 },
        ]);
        var simulator = new MacOSInputSimulator(
            requestPostEventAccess: static () => true,
            isMacOS: static () => true,
            getCursorPosition: () => cursorPositions.Dequeue(),
            postMouseMovement: static (_, _, _) => true,
            postMouseButton: (_, _, x, y) =>
            {
                buttonPosition = (x, y);
                return true;
            });
        simulator.Initialize();

        simulator.MouseButton(MouseButtonCode.Left, pressed: true);

        Assert.Equal((30, 40), buttonPosition);
    }

    [Fact]
    public void MouseButton_ReleaseAfterPress_ReusesTrackedPressPosition()
    {
        var buttonPositions = new List<(bool Pressed, int X, int Y)>();
        var cursorQueries = 0;
        var simulator = new MacOSInputSimulator(
            requestPostEventAccess: static () => true,
            isMacOS: static () => true,
            getCursorPosition: () =>
            {
                cursorQueries++;
                return new CoreGraphics.CGPoint { X = 70, Y = 80 };
            },
            postMouseMovement: static (_, _, _) => true,
            postMouseButton: (_, pressed, x, y) =>
            {
                buttonPositions.Add((pressed, x, y));
                return true;
            });
        simulator.Initialize();

        simulator.MouseButton(MouseButtonCode.Left, pressed: true);
        simulator.MouseButton(MouseButtonCode.Left, pressed: false);

        Assert.Equal(2, cursorQueries);
        Assert.Equal([(true, 70, 80), (false, 70, 80)], buttonPositions);
    }

    private delegate bool TryCreateSystemDefinedCGEventDelegate(
        MacOSSystemKeyEventPayload payload,
        out IntPtr eventRef);
}
