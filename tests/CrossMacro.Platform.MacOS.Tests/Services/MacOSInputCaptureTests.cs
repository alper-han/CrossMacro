using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Services;
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
