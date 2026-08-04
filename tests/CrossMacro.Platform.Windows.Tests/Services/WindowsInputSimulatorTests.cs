
namespace CrossMacro.Platform.Windows.Tests.Services;

public sealed class WindowsInputSimulatorTests
{
    [Fact]
    public void ProviderName_IsExpected()
    {
        var simulator = new WindowsInputSimulator();

        Assert.Equal("Windows SendInput", simulator.ProviderName);
    }

    [Fact]
    public void IsSupported_MatchesCurrentPlatform()
    {
        var simulator = new WindowsInputSimulator();

        Assert.Equal(OperatingSystem.IsWindows(), simulator.IsSupported);
    }

    [Fact]
    public void SupportsUnicodeTextInput_MatchesPlatformSupport()
    {
        var simulator = new WindowsInputSimulator();

        _ = Assert.IsAssignableFrom<IUnicodeTextInputSimulator>(simulator);
        _ = Assert.IsAssignableFrom<ITaggedKeyboardInputSimulator>(simulator);
        _ = Assert.IsAssignableFrom<ITaggedUnicodeTextInputSimulator>(simulator);
        Assert.Equal(simulator.IsSupported, simulator.SupportsUnicodeTextInput);
        Assert.Equal(simulator.IsSupported, simulator.SupportsTaggedKeyboardInput);
    }

    [Theory]
    [InlineData(InputEventCode.BTN_SIDE, true, MouseEventFlags.MOUSEEVENTF_XDOWN, User32.XBUTTON1)]
    [InlineData(InputEventCode.BTN_SIDE, false, MouseEventFlags.MOUSEEVENTF_XUP, User32.XBUTTON1)]
    [InlineData(InputEventCode.BTN_EXTRA, true, MouseEventFlags.MOUSEEVENTF_XDOWN, User32.XBUTTON2)]
    [InlineData(InputEventCode.BTN_EXTRA, false, MouseEventFlags.MOUSEEVENTF_XUP, User32.XBUTTON2)]
    public void TryCreateMouseButtonInput_WhenExtendedButton_UsesXButtonInput(int button, bool pressed, uint expectedFlags, ushort expectedMouseData)
    {
        var created = WindowsInputSimulator.TryCreateMouseButtonInput(button, pressed, out var input);

        Assert.True(created);
        Assert.Equal(InputType.INPUT_MOUSE, input.type);
        Assert.Equal(expectedFlags, input.U.mi.dwFlags);
        Assert.Equal((uint)expectedMouseData, input.U.mi.mouseData);
    }

    [Theory]
    [InlineData(InputEventCode.KEY_ENTER, true, 0u)]
    [InlineData(InputEventCode.KEY_ENTER, false, KeyEventFlags.KEYEVENTF_KEYUP)]
    [InlineData(InputEventCode.KEY_KPENTER, true, KeyEventFlags.KEYEVENTF_EXTENDEDKEY)]
    [InlineData(InputEventCode.KEY_KPENTER, false, KeyEventFlags.KEYEVENTF_EXTENDEDKEY | KeyEventFlags.KEYEVENTF_KEYUP)]
    public void TryCreateKeyboardInput_WhenCreatingEnter_UsesReturnAndCorrectFlags(
        int keyCode,
        bool pressed,
        uint expectedFlags)
    {
        var created = WindowsInputSimulator.TryCreateKeyboardInput(keyCode, pressed, out var input);

        Assert.True(created);
        Assert.Equal(InputType.INPUT_KEYBOARD, input.type);
        Assert.Equal((ushort)0x0D, input.U.ki.wVk);
        Assert.Equal(expectedFlags, input.U.ki.dwFlags);
    }

    [Theory]
    [InlineData(User32.WM_XBUTTONDOWN, 1, true, MouseEventFlags.MOUSEEVENTF_XDOWN, User32.XBUTTON1)]
    [InlineData(User32.WM_XBUTTONUP, 1, false, MouseEventFlags.MOUSEEVENTF_XUP, User32.XBUTTON1)]
    [InlineData(User32.WM_XBUTTONDOWN, 2, true, MouseEventFlags.MOUSEEVENTF_XDOWN, User32.XBUTTON2)]
    [InlineData(User32.WM_XBUTTONUP, 2, false, MouseEventFlags.MOUSEEVENTF_XUP, User32.XBUTTON2)]
    public void CaptureXButtonMapping_WhenPlayedBack_UsesMatchingXButtonInput(
        uint message,
        ushort xButton,
        bool pressed,
        uint expectedFlags,
        ushort expectedMouseData)
    {
        var mapped = WindowsInputCapture.TryMapMouseButtonOrScroll(
            message,
            (uint)xButton << 16,
            out var button,
            out var value,
            out var type);

        Assert.True(mapped);
        Assert.Equal(pressed ? 1 : 0, value);
        Assert.Equal(InputEventCode.EV_KEY, type);

        var created = WindowsInputSimulator.TryCreateMouseButtonInput(button, pressed, out var input);

        Assert.True(created);
        Assert.Equal(InputType.INPUT_MOUSE, input.type);
        Assert.Equal(expectedFlags, input.U.mi.dwFlags);
        Assert.Equal((uint)expectedMouseData, input.U.mi.mouseData);
    }

    [Fact]
    public void TryCreateMouseButtonInput_WhenButtonIsUnknown_ReturnsFalse()
    {
        var created = WindowsInputSimulator.TryCreateMouseButtonInput(-1, pressed: true, out _);

        Assert.False(created);
    }

    [Theory]
    [InlineData(-120, 0xFFFFFF88u)]
    [InlineData(-1, 0xFFFFFF88u)]
    public void CreateScrollInput_WhenHorizontalDeltaIsNegative_UsesHorizontalWheelAndUncheckedMouseData(
        int delta,
        uint expectedMouseData)
    {
        var input = WindowsInputSimulator.CreateScrollInput(delta, isHorizontal: true);

        Assert.Equal(InputType.INPUT_MOUSE, input.type);
        Assert.Equal(MouseEventFlags.MOUSEEVENTF_HWHEEL, input.U.mi.dwFlags);
        Assert.Equal(expectedMouseData, input.U.mi.mouseData);
    }

    [Fact]
    public void CreateScrollInput_WhenVerticalDeltaIsPositive_UsesVerticalWheelAndNormalizedMouseData()
    {
        var input = WindowsInputSimulator.CreateScrollInput(1, isHorizontal: false);

        Assert.Equal(InputType.INPUT_MOUSE, input.type);
        Assert.Equal(MouseEventFlags.MOUSEEVENTF_WHEEL, input.U.mi.dwFlags);
        Assert.Equal(120u, input.U.mi.mouseData);
    }

    [Theory]
    [InlineData(-1920, -200, 0, 0)]
    [InlineData(2559, 1439, 65535, 65535)]
    public void CreateAbsoluteMouseInput_ShouldNormalizeEntireVirtualDesktop(
        int x,
        int y,
        int expectedX,
        int expectedY)
    {
        var input = WindowsInputSimulator.CreateAbsoluteMouseInput(
            x,
            y,
            new ScreenRect(-1920, -200, 4480, 1640));

        Assert.Equal(expectedX, input.U.mi.dx);
        Assert.Equal(expectedY, input.U.mi.dy);
        Assert.Equal(
            MouseEventFlags.MOUSEEVENTF_MOVE
            | MouseEventFlags.MOUSEEVENTF_ABSOLUTE
            | MouseEventFlags.MOUSEEVENTF_VIRTUALDESK
            | MouseEventFlags.MOUSEEVENTF_MOVE_NOCOALESCE,
            input.U.mi.dwFlags);
    }

    [Fact]
    public void ReadDesktopBounds_ShouldUseVirtualScreenMetricsIncludingOrigin()
    {
        var metrics = new Dictionary<int, int>
        {
            [User32.SM_XVIRTUALSCREEN] = -1920,
            [User32.SM_YVIRTUALSCREEN] = -200,
            [User32.SM_CXVIRTUALSCREEN] = 4480,
            [User32.SM_CYVIRTUALSCREEN] = 1640,
        };

        var bounds = WindowsMousePositionProvider.ReadDesktopBounds(metric => metrics[metric]);

        Assert.Equal(new ScreenRect(-1920, -200, 4480, 1640), bounds);
    }
}
