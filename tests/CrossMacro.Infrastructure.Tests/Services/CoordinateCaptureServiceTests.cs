namespace CrossMacro.Infrastructure.Tests.Services;

using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Platform.Abstractions;
using CrossMacro.TestInfrastructure;
using FluentAssertions;
using NSubstitute;

public class CoordinateCaptureServiceTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(2);

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
        await capture.ConfiguredSignal.WaitAsync(TestTimeout);

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
        await capture.ConfiguredSignal.WaitAsync(TestTimeout);

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
        await capture.ConfiguredSignal.WaitAsync(TestTimeout);

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
        await capture.ConfiguredSignal.WaitAsync(TestTimeout);

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

    [Fact]
    public async Task CaptureMousePositionAsync_WhenCaptureStartFaultsAsynchronously_ReturnsNull()
    {
        var positionProvider = Substitute.For<IMousePositionProvider>();
        var capture = new FakeInputCapture { ReturnFaultedStartTask = true };
        var service = new CoordinateCaptureService(positionProvider, () => capture);

        var result = await service.CaptureMousePositionAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task CaptureMousePositionAsync_WhenSecondCaptureStarts_FirstCaptureDoesNotClearCurrentState()
    {
        var positionProvider = Substitute.For<IMousePositionProvider>();
        positionProvider.GetAbsolutePositionAsync().Returns(Task.FromResult<(int X, int Y)?>(new(100, 200)));

        var firstCapture = new FakeInputCapture();
        var secondCapture = new FakeInputCapture();
        var factoryCalls = 0;
        var service = new CoordinateCaptureService(positionProvider, () => ++factoryCalls == 1 ? firstCapture : secondCapture);

        var firstTask = service.CaptureMousePositionAsync();
        await firstCapture.ConfiguredSignal.WaitAsync(TestTimeout);

        var secondTask = service.CaptureMousePositionAsync();
        await secondCapture.ConfiguredSignal.WaitAsync(TestTimeout);

        var firstResult = await firstTask;
        firstResult.Should().BeNull();
        service.IsCapturing.Should().BeTrue();

        secondCapture.EmitInput(new InputCaptureEventArgs
        {
            Type = InputEventType.Key,
            Code = InputEventCode.KEY_ENTER,
            Value = 1
        });

        var secondResult = await secondTask;
        secondResult.Should().Be((100, 200));
        service.IsCapturing.Should().BeFalse();
    }

    private sealed class FakeInputCapture : IInputCapture
    {
        public string ProviderName => "FakeCapture";
        public bool IsSupported => true;
        public bool ThrowOnStart { get; init; }
        public bool ReturnFaultedStartTask { get; init; }
        public AsyncSignal ConfiguredSignal { get; } = new();
        public int ConfigureCalls { get; private set; }
        public bool LastCaptureMouse { get; private set; }
        public bool LastCaptureKeyboard { get; private set; }

        public event EventHandler<InputCaptureEventArgs>? InputReceived;
        public event EventHandler<string>? Error
        {
            add { }
            remove { }
        }

        public void Configure(bool captureMouse, bool captureKeyboard)
        {
            ConfigureCalls++;
            LastCaptureMouse = captureMouse;
            LastCaptureKeyboard = captureKeyboard;
            ConfiguredSignal.Signal();
        }

        public Task StartAsync(CancellationToken ct)
        {
            if (ThrowOnStart)
            {
                throw new InvalidOperationException("capture start failed");
            }

            if (ReturnFaultedStartTask)
            {
                return Task.FromException(new InvalidOperationException("capture start failed"));
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
