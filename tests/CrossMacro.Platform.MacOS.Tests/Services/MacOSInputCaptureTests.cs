using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.MacOS.Native;
using CrossMacro.Platform.MacOS.Services;
using CrossMacro.TestInfrastructure;
using Xunit;

namespace CrossMacro.Platform.MacOS.Tests.Services;

public class MacOSInputCaptureTests
{
    [Fact]
    public void ShouldIgnoreKeyboardEvent_RecognizesOnlyCrossMacroMarker()
    {
        Assert.True(MacOSInputCapture.ShouldIgnoreKeyboardEvent(InputEventMarkers.TextExpansionKeyboardEvent));
        Assert.False(MacOSInputCapture.ShouldIgnoreKeyboardEvent(0));
        Assert.False(MacOSInputCapture.ShouldIgnoreKeyboardEvent(123));
    }

    [Fact]
    public void GetCurrentTimestamp_UsesUnixMillisecondsScale()
    {
        long before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        long timestamp = MacOSInputCapture.GetCurrentTimestamp();

        long after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Assert.InRange(timestamp, before, after);
    }


    [Fact]
    public void TryCreateKeyboardInput_WhenNativeKeyIsUnknown_ReturnsNoMatchWithoutCodeZeroEvent()
    {
        bool created = MacOSInputCapture.TryCreateKeyboardInput(
            CoreGraphics.CGEventType.KeyDown,
            0xFFFF,
            default,
            timestamp: 123,
            out _);

        Assert.False(created);
    }

    [Fact]
    public void TryCreateKeyboardInput_WhenNativeKeyIsKnown_CreatesKeyEvent()
    {
        bool created = MacOSInputCapture.TryCreateKeyboardInput(
            CoreGraphics.CGEventType.KeyDown,
            0x00,
            default,
            timestamp: 123,
            out var inputEvent);

        Assert.True(created);
        Assert.Equal(InputEventType.Key, inputEvent.Type);
        Assert.Equal(InputEventCode.KEY_A, inputEvent.Code);
        Assert.Equal(1, inputEvent.Value);
        Assert.Equal(123, inputEvent.Timestamp);
    }

    [Theory]
    [InlineData(16, InputEventCode.KEY_PLAYPAUSE)]
    [InlineData(17, InputEventCode.KEY_NEXTSONG)]
    [InlineData(18, InputEventCode.KEY_PREVIOUSSONG)]
    public void TryCreateSystemDefinedInput_WhenSupportedMediaKeyIsPressed_CreatesKeyDownEvent(int keyType, int expectedCode)
    {
        bool created = MacOSInputCapture.TryCreateSystemDefinedInput(
            CoreGraphics.CGEventType.SystemDefined,
            subtype: 8,
            data1: CreateSystemDefinedData1(keyType, 0x0A),
            timestamp: 123,
            out var inputEvent);

        Assert.True(created);
        Assert.Equal(InputEventType.Key, inputEvent.Type);
        Assert.Equal(expectedCode, inputEvent.Code);
        Assert.Equal(1, inputEvent.Value);
        Assert.Equal(123, inputEvent.Timestamp);
    }

    [Theory]
    [InlineData(16, InputEventCode.KEY_PLAYPAUSE)]
    [InlineData(17, InputEventCode.KEY_NEXTSONG)]
    [InlineData(18, InputEventCode.KEY_PREVIOUSSONG)]
    public void TryCreateSystemDefinedInput_WhenSupportedMediaKeyIsReleased_CreatesKeyUpEvent(int keyType, int expectedCode)
    {
        bool created = MacOSInputCapture.TryCreateSystemDefinedInput(
            CoreGraphics.CGEventType.SystemDefined,
            subtype: 8,
            data1: CreateSystemDefinedData1(keyType, 0x0B),
            timestamp: 456,
            out var inputEvent);

        Assert.True(created);
        Assert.Equal(InputEventType.Key, inputEvent.Type);
        Assert.Equal(expectedCode, inputEvent.Code);
        Assert.Equal(0, inputEvent.Value);
        Assert.Equal(456, inputEvent.Timestamp);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(14)]
    [InlineData(19)]
    [InlineData(20)]
    [InlineData(21)]
    [InlineData(22)]
    [InlineData(23)]
    public void TryCreateSystemDefinedInput_WhenKeyTypeIsUnsupported_ReturnsNoMatchWithoutCodeZeroEvent(int keyType)
    {
        bool created = MacOSInputCapture.TryCreateSystemDefinedInput(
            CoreGraphics.CGEventType.SystemDefined,
            subtype: 8,
            data1: CreateSystemDefinedData1(keyType, 0x0A),
            timestamp: 123,
            out _);

        Assert.False(created);
    }

    [Theory]
    [InlineData(10, 8, 16, 0x0A)]
    [InlineData(14, 7, 16, 0x0A)]
    [InlineData(14, 8, 16, 0x09)]
    public void TryCreateSystemDefinedInput_WhenPayloadIsNotAuditedSubtype8PressOrRelease_ReturnsNoMatch(
        int eventType,
        long subtype,
        int keyType,
        int state)
    {
        bool created = MacOSInputCapture.TryCreateSystemDefinedInput(
            (CoreGraphics.CGEventType)eventType,
            subtype,
            CreateSystemDefinedData1(keyType, state),
            timestamp: 123,
            out _);

        Assert.False(created);
    }

    [Theory]
    [InlineData(0x65)]
    [InlineData(0x6D)]
    public void TryCreateSystemDefinedInput_WhenPayloadLooksLikeFunctionKeyVirtualKey_ReturnsNoMatch(int keyType)
    {
        bool created = MacOSInputCapture.TryCreateSystemDefinedInput(
            CoreGraphics.CGEventType.SystemDefined,
            subtype: 8,
            data1: CreateSystemDefinedData1(keyType, 0x0A),
            timestamp: 123,
            out _);

        Assert.False(created);
    }

    [Fact]
    public void TryCreateKeyboardInput_WhenOrdinaryFunctionKeyIsKnown_RemainsKeyMapBacked()
    {
        bool created = MacOSInputCapture.TryCreateKeyboardInput(
            CoreGraphics.CGEventType.KeyDown,
            0x65,
            default,
            timestamp: 123,
            out var inputEvent);

        Assert.True(created);
        Assert.Equal(InputEventCode.KEY_F9, inputEvent.Code);
        Assert.Equal(1, inputEvent.Value);
    }

    private static long CreateSystemDefinedData1(int keyType, int state)
    {
        return (keyType << 16) | (state << 8);
    }

    [MacOSFact]
    public void IsSupported_OnMacOS_ShouldBeTrue()
    {
        using var capture = new MacOSInputCapture();

        Assert.True(capture.IsSupported);
    }

    [MacOSFact]
    public async Task StartAsync_WithCancelledToken_ShouldThrowOperationCanceledException()
    {
        using var capture = new MacOSInputCapture();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => capture.StartAsync(cts.Token));
    }

    [NonMacOSFact]
    public async Task StartAsync_OnNonMacOS_ShouldReturnWithoutThrowingAndRaiseError()
    {
        using var capture = new MacOSInputCapture();
        string? error = null;
        capture.Error += (_, message) => error = message;

        var exception = await Record.ExceptionAsync(() => capture.StartAsync(CancellationToken.None));

        Assert.Null(exception);
        Assert.NotNull(error);
        Assert.Contains("only supported on macOS", error, StringComparison.OrdinalIgnoreCase);
    }

    [NonMacOSFact]
    public async Task StartAsync_CalledMultipleTimesOnNonMacOS_ShouldNotThrow()
    {
        using var capture = new MacOSInputCapture();

        await capture.StartAsync(CancellationToken.None);
        var exception = await Record.ExceptionAsync(() => capture.StartAsync(CancellationToken.None));

        Assert.Null(exception);
    }
}
