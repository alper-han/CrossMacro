namespace CrossMacro.Infrastructure.Tests.Services;

using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services;
using FluentAssertions;
using NSubstitute;

public class CoordinateCaptureServiceTests
{
    [Fact]
    public async Task CaptureMousePositionAsync_WhenFactoryMissing_ReturnsCurrentPosition()
    {
        var positionProvider = Substitute.For<IMousePositionProvider>();
        positionProvider.GetAbsolutePositionAsync().Returns(Task.FromResult<(int X, int Y)?>(new(42, 84)));
        var service = new CoordinateCaptureService(positionProvider, inputCaptureFactory: null);

        var result = await service.CaptureMousePositionAsync();

        result.Should().Be((42, 84));
    }

    [Fact]
    public async Task CaptureMousePositionAsync_WhenEnterPressed_ReturnsCurrentMousePosition()
    {
        var positionProvider = Substitute.For<IMousePositionProvider>();
        positionProvider.GetAbsolutePositionAsync().Returns(Task.FromResult<(int X, int Y)?>(new(100, 200)));

        var capture = new FakeInputCapture();
        var service = new CoordinateCaptureService(positionProvider, () => capture);

        var captureTask = service.CaptureMousePositionAsync();
        await WaitForConditionAsync(() => capture.ConfigureCalls > 0);

        capture.EmitInput(new InputCaptureEventArgs
        {
            Type = InputEventType.Key,
            Code = InputEventCode.KEY_ENTER,
            Value = 1
        });

        var result = await captureTask;

        result.Should().Be((100, 200));
        capture.LastCaptureMouse.Should().BeTrue();
        capture.LastCaptureKeyboard.Should().BeTrue();
    }

    [Fact]
    public async Task CaptureMousePositionAsync_WhenEscapePressed_ReturnsNull()
    {
        var positionProvider = Substitute.For<IMousePositionProvider>();
        var capture = new FakeInputCapture();
        var service = new CoordinateCaptureService(positionProvider, () => capture);

        var captureTask = service.CaptureMousePositionAsync();
        await WaitForConditionAsync(() => capture.ConfigureCalls > 0);

        capture.EmitInput(new InputCaptureEventArgs
        {
            Type = InputEventType.Key,
            Code = InputEventCode.KEY_ESC,
            Value = 1
        });

        var result = await captureTask;

        result.Should().BeNull();
    }

    [Fact]
    public async Task CaptureKeyCodeAsync_WhenAnyKeyPressed_ReturnsKeyCode()
    {
        var positionProvider = Substitute.For<IMousePositionProvider>();
        var capture = new FakeInputCapture();
        var service = new CoordinateCaptureService(positionProvider, () => capture);

        var captureTask = service.CaptureKeyCodeAsync();
        await WaitForConditionAsync(() => capture.ConfigureCalls > 0);

        capture.EmitInput(new InputCaptureEventArgs
        {
            Type = InputEventType.Key,
            Code = InputEventCode.KEY_ESC,
            Value = 1
        });

        var result = await captureTask;

        result.Should().Be(InputEventCode.KEY_ESC);
        capture.LastCaptureMouse.Should().BeFalse();
        capture.LastCaptureKeyboard.Should().BeTrue();
    }

    [Fact]
    public async Task CancelCapture_WhenCaptureIsActive_CompletesPendingCaptureWithNull()
    {
        var positionProvider = Substitute.For<IMousePositionProvider>();
        var capture = new FakeInputCapture();
        var service = new CoordinateCaptureService(positionProvider, () => capture);

        var captureTask = service.CaptureMousePositionAsync();
        await WaitForConditionAsync(() => service.IsCapturing);

        service.CancelCapture();
        var result = await captureTask;

        result.Should().BeNull();
        service.IsCapturing.Should().BeFalse();
    }

    [Fact]
    public async Task CaptureMousePositionAsync_WhenCaptureStartThrows_ReturnsNull()
    {
        var positionProvider = Substitute.For<IMousePositionProvider>();
        var capture = new FakeInputCapture { ThrowOnStart = true };
        var service = new CoordinateCaptureService(positionProvider, () => capture);

        var result = await service.CaptureMousePositionAsync();

        result.Should().BeNull();
    }

    private static async Task WaitForConditionAsync(Func<bool> condition, int maxAttempts = 50, int delayMs = 10)
    {
        for (var i = 0; i < maxAttempts; i++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(delayMs);
        }

        throw new TimeoutException("Condition was not met in expected time.");
    }

    private sealed class FakeInputCapture : IInputCapture
    {
        public string ProviderName => "FakeCapture";
        public bool IsSupported => true;
        public bool ThrowOnStart { get; init; }
        public int ConfigureCalls { get; private set; }
        public bool LastCaptureMouse { get; private set; }
        public bool LastCaptureKeyboard { get; private set; }

        public event EventHandler<InputCaptureEventArgs>? InputReceived;
        public event EventHandler<string>? Error;

        public void Configure(bool captureMouse, bool captureKeyboard)
        {
            ConfigureCalls++;
            LastCaptureMouse = captureMouse;
            LastCaptureKeyboard = captureKeyboard;
        }

        public Task StartAsync(CancellationToken ct)
        {
            if (ThrowOnStart)
            {
                throw new InvalidOperationException("capture start failed");
            }

            return Task.CompletedTask;
        }

        public void Stop()
        {
        }

        public void EmitInput(InputCaptureEventArgs args)
        {
            InputReceived?.Invoke(this, args);
        }

        public void Dispose()
        {
        }
    }
}
