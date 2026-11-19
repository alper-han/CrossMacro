using System.Collections.Concurrent;
using System.Globalization;
using CrossMacro.Core.Logging;

namespace CrossMacro.Platform.Windows.Tests.Services;

public sealed class WindowsHookEventTranslatorTests
{
    private const ushort VkA = 0x41;
    private const ushort VkUnmapped = 0xFF;

    [Fact]
    public void OnKeyboardHook_WhenKeyIsPressedAndReleased_EmitsKeyDownAndUp()
    {
        var (translator, events) = Create();

        translator.OnKeyboardHook(User32.WM_KEYDOWN, Key(VkA));
        translator.OnKeyboardHook(User32.WM_KEYUP, Key(VkA));

        Assert.Collection(
            events,
            down => AssertKey(down, InputEventCode.KEY_A, value: 1),
            up => AssertKey(up, InputEventCode.KEY_A, value: 0));
    }

    [Fact]
    public void OnKeyboardHook_WhenEventWasInjectedByCrossMacro_IsIgnored()
    {
        var (translator, events) = Create();
        var injected = Key(VkA) with
        {
            flags = 0x10,
            dwExtraInfo = InputEventMarkers.ToIntPtr(InputEventMarkers.TextExpansionKeyboardEvent),
        };

        translator.OnKeyboardHook(User32.WM_KEYDOWN, injected);

        Assert.Empty(events);
    }

    [Fact]
    public void OnKeyboardHook_NeverWritesKeyPressesToTheLog()
    {
        var logger = new RecordingCoreLogger();
        using var scope = Log.PushLogger(logger);
        var (translator, _) = Create();

        translator.OnKeyboardHook(User32.WM_KEYDOWN, Key(VkA));
        translator.OnKeyboardHook(User32.WM_KEYUP, Key(VkA));

        Assert.DoesNotContain(logger.Entries, static entry => entry.Contains("[WindowsInputCapture]", StringComparison.Ordinal));
    }

    [Fact]
    public void OnKeyboardHook_WhenKeyIsUnmapped_WarnsOncePerKeyAndEmitsNothing()
    {
        var logger = new RecordingCoreLogger();
        using var scope = Log.PushLogger(logger);
        var (translator, events) = Create();

        translator.OnKeyboardHook(User32.WM_KEYDOWN, Key(VkUnmapped));
        translator.OnKeyboardHook(User32.WM_KEYUP, Key(VkUnmapped));
        translator.OnKeyboardHook(User32.WM_KEYDOWN, Key(VkUnmapped));

        Assert.Empty(events);
        Assert.Single(logger.Entries, static entry => entry.Contains("[WindowsInputCapture] Unmapped", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(User32.WM_LBUTTONDOWN, InputEventCode.BTN_LEFT, 1)]
    [InlineData(User32.WM_LBUTTONUP, InputEventCode.BTN_LEFT, 0)]
    [InlineData(User32.WM_RBUTTONDOWN, InputEventCode.BTN_RIGHT, 1)]
    [InlineData(User32.WM_MBUTTONUP, InputEventCode.BTN_MIDDLE, 0)]
    public void OnMouseHook_WhenButtonMessage_EmitsMouseButton(uint message, ushort code, int value)
    {
        var (translator, events) = Create();

        translator.OnMouseHook(message, Mouse(0, 0));

        var emitted = Assert.Single(events);
        Assert.Equal(InputEventType.MouseButton, emitted.Type);
        Assert.Equal(code, emitted.Code);
        Assert.Equal(value, emitted.Value);
    }

    [Theory]
    [InlineData(120, 1)]
    [InlineData(-120, -1)]
    [InlineData(360, 3)]
    public void OnMouseHook_WhenWheelTurnsWholeNotches_EmitsNotchesNotWheelDelta(short wheelDelta, int expectedNotches)
    {
        var (translator, events) = Create();

        translator.OnMouseHook(User32.WM_MOUSEWHEEL, Wheel(wheelDelta));

        var emitted = Assert.Single(events);
        Assert.Equal(InputEventType.MouseScroll, emitted.Type);
        Assert.Equal(InputEventCode.REL_WHEEL, emitted.Code);
        Assert.Equal(expectedNotches, emitted.Value);
    }

    [Fact]
    public void OnMouseHook_WhenPrecisionWheelSendsFractions_EmitsOneNotchOnceItAddsUp()
    {
        var (translator, events) = Create();

        translator.OnMouseHook(User32.WM_MOUSEWHEEL, Wheel(40));
        translator.OnMouseHook(User32.WM_MOUSEWHEEL, Wheel(40));
        Assert.Empty(events);

        translator.OnMouseHook(User32.WM_MOUSEWHEEL, Wheel(40));

        var emitted = Assert.Single(events);
        Assert.Equal(1, emitted.Value);
    }

    [Fact]
    public void OnMouseHook_KeepsVerticalAndHorizontalRemaindersSeparate()
    {
        var (translator, events) = Create();

        translator.OnMouseHook(User32.WM_MOUSEWHEEL, Wheel(60));
        translator.OnMouseHook(User32.WM_MOUSEHWHEEL, Wheel(60));
        Assert.Empty(events);

        translator.OnMouseHook(User32.WM_MOUSEHWHEEL, Wheel(60));

        var emitted = Assert.Single(events);
        Assert.Equal(InputEventCode.REL_HWHEEL, emitted.Code);
        Assert.Equal(1, emitted.Value);
    }

    [Fact]
    public void OnMouseHook_InAbsoluteMode_EmitsPositionAndSync()
    {
        var (translator, events) = Create(useAbsoluteCoordinates: true);

        translator.OnMouseHook(User32.WM_MOUSEMOVE, Mouse(300, 200));

        Assert.Collection(
            events,
            x => AssertMove(x, InputEventCode.ABS_X, 300),
            y => AssertMove(y, InputEventCode.ABS_Y, 200),
            sync => Assert.Equal(InputEventType.Sync, sync.Type));
    }

    [Fact]
    public void OnMouseHook_InLogicalRelativeMode_EmitsOnlyChangedAxes()
    {
        var (translator, events) = Create();
        translator.SetCursorPosition(100, 100);

        translator.OnMouseHook(User32.WM_MOUSEMOVE, Mouse(110, 100));

        Assert.Collection(
            events,
            x => AssertMove(x, InputEventCode.REL_X, 10),
            sync => Assert.Equal(InputEventType.Sync, sync.Type));
    }

    [Fact]
    public void OnMouseHook_WhenCursorPositionIsUnknown_UsesFirstMoveAsBaselineInRelativeMode()
    {
        var (translator, events) = Create();

        translator.OnMouseHook(User32.WM_MOUSEMOVE, Mouse(50, 50));
        Assert.Empty(events);

        translator.OnMouseHook(User32.WM_MOUSEMOVE, Mouse(45, 50));

        Assert.Collection(
            events,
            x => AssertMove(x, InputEventCode.REL_X, -5),
            sync => Assert.Equal(InputEventType.Sync, sync.Type));
    }

    [Fact]
    public void OnMouseHook_WhenCursorDidNotMove_EmitsNothing()
    {
        var (translator, events) = Create(useAbsoluteCoordinates: true);
        translator.SetCursorPosition(10, 10);

        translator.OnMouseHook(User32.WM_MOUSEMOVE, Mouse(10, 10));

        Assert.Empty(events);
    }

    [Fact]
    public void OnMouseHook_InRawMode_LeavesMovementToRawInput()
    {
        var (translator, events) = Create(useRawRelativeCoordinates: true);

        translator.OnMouseHook(User32.WM_MOUSEMOVE, Mouse(10, 10));

        Assert.Empty(events);
    }

    [Fact]
    public void OnRawMouseMovement_EmitsRelativeAxesAndSync()
    {
        var (translator, events) = Create(useRawRelativeCoordinates: true);

        translator.OnRawMouseMovement(3, 0);
        translator.OnRawMouseMovement(0, -4);

        Assert.Collection(
            events,
            x => AssertMove(x, InputEventCode.REL_X, 3),
            sync => Assert.Equal(InputEventType.Sync, sync.Type),
            y => AssertMove(y, InputEventCode.REL_Y, -4),
            sync => Assert.Equal(InputEventType.Sync, sync.Type));
    }

    private static (WindowsHookEventTranslator Translator, List<CapturedInputEvent> Events) Create(
        bool useAbsoluteCoordinates = false,
        bool useRawRelativeCoordinates = false)
    {
        var events = new List<CapturedInputEvent>();
        return (new WindowsHookEventTranslator(events.Add, useAbsoluteCoordinates, useRawRelativeCoordinates), events);
    }

    private static KbdllHookStruct Key(ushort virtualKey) => new() { vkCode = virtualKey };

    private static MsllHookStruct Mouse(int x, int y) => new() { pt = new PointStruct { x = x, y = y } };

    private static MsllHookStruct Wheel(short delta) => new() { mouseData = (uint)(ushort)delta << 16 };

    private static void AssertKey(CapturedInputEvent inputEvent, int code, int value)
    {
        Assert.Equal(InputEventType.Key, inputEvent.Type);
        Assert.Equal(code, inputEvent.Code);
        Assert.Equal(value, inputEvent.Value);
    }

    private static void AssertMove(CapturedInputEvent inputEvent, int code, int value)
    {
        Assert.Equal(InputEventType.MouseMove, inputEvent.Type);
        Assert.Equal(code, inputEvent.Code);
        Assert.Equal(value, inputEvent.Value);
    }

    private sealed class RecordingCoreLogger : ICoreLogger
    {
        private readonly ConcurrentQueue<string> _entries = new();

        public IReadOnlyCollection<string> Entries => _entries;

        public bool IsEnabled(CoreLogLevel level) => true;

        public void Verbose(string messageTemplate, params object?[] propertyValues) => Record(messageTemplate, propertyValues);

        public void Verbose(Exception exception, string messageTemplate, params object?[] propertyValues) => Record(messageTemplate, propertyValues);

        public void Debug(string messageTemplate, params object?[] propertyValues) => Record(messageTemplate, propertyValues);

        public void Debug(Exception exception, string messageTemplate, params object?[] propertyValues) => Record(messageTemplate, propertyValues);

        public void Information(string messageTemplate, params object?[] propertyValues) => Record(messageTemplate, propertyValues);

        public void Information(Exception exception, string messageTemplate, params object?[] propertyValues) => Record(messageTemplate, propertyValues);

        public void Warning(string messageTemplate, params object?[] propertyValues) => Record(messageTemplate, propertyValues);

        public void Warning(Exception exception, string messageTemplate, params object?[] propertyValues) => Record(messageTemplate, propertyValues);

        public void LogError(string messageTemplate, params object?[] propertyValues) => Record(messageTemplate, propertyValues);

        public void LogError(Exception exception, string messageTemplate, params object?[] propertyValues) => Record(messageTemplate, propertyValues);

        public void Fatal(string messageTemplate, params object?[] propertyValues) => Record(messageTemplate, propertyValues);

        public void Fatal(Exception exception, string messageTemplate, params object?[] propertyValues) => Record(messageTemplate, propertyValues);

        private void Record(string messageTemplate, object?[] propertyValues) =>
            _entries.Enqueue(string.Join(' ', messageTemplate, string.Join(' ', propertyValues)));
    }
}
