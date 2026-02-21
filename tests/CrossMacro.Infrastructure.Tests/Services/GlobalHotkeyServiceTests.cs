using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace CrossMacro.Infrastructure.Tests.Services;

public class GlobalHotkeyServiceTests
{
    private readonly IHotkeyConfigurationService _config;
    private readonly IHotkeyParser _parser;
    private readonly IHotkeyMatcher _matcher;
    private readonly IModifierStateTracker _modifierTracker;
    private readonly IHotkeyStringBuilder _stringBuilder;
    private readonly IMouseButtonMapper _mouseButtonMapper;
    private readonly IInputCapture _inputCapture;
    private readonly GlobalHotkeyService _service;

    public GlobalHotkeyServiceTests()
    {
        _config = Substitute.For<IHotkeyConfigurationService>();
        _config.Load().Returns(new HotkeySettings 
        { 
            RecordingHotkey = "F9", 
            PlaybackHotkey = "F10", 
            PauseHotkey = "F11" 
        });

        // Mock new services
        _parser = Substitute.For<IHotkeyParser>();
        _matcher = Substitute.For<IHotkeyMatcher>();
        _modifierTracker = Substitute.For<IModifierStateTracker>();
        _stringBuilder = Substitute.For<IHotkeyStringBuilder>();
        _mouseButtonMapper = Substitute.For<IMouseButtonMapper>();

        // Setup Parser behavior to return valid mappings
        var f9Mapping = new HotkeyMapping { MainKey = 67 }; // F9
        var f10Mapping = new HotkeyMapping { MainKey = 68 }; // F10
        var f11Mapping = new HotkeyMapping { MainKey = 69 }; // F11
        var numpadMapping = new HotkeyMapping { MainKey = 82 }; // Numpad0

        _parser.Parse("F9").Returns(f9Mapping);
        _parser.Parse("F10").Returns(f10Mapping);
        _parser.Parse("F11").Returns(f11Mapping);
        _parser.Parse("Numpad0").Returns(numpadMapping);

        // Setup Matcher behavior
        _matcher.TryMatch(67, Arg.Any<IReadOnlySet<int>>(), f9Mapping, "Recording").Returns(true);
        _matcher.TryMatch(Arg.Is<int>(x => x != 67), Arg.Any<IReadOnlySet<int>>(), Arg.Any<HotkeyMapping>(), Arg.Any<string>()).Returns(false);

        // Setup Modifier Tracker
        _modifierTracker.CurrentModifiers.Returns(new HashSet<int>());

        _mouseButtonMapper.GetMouseButtonName(InputEventCode.BTN_EXTRA).Returns("Mouse Extra");
        _stringBuilder.BuildForMouse("Mouse Extra", Arg.Any<IReadOnlySet<int>>()).Returns("Mouse Extra");

        _inputCapture = Substitute.For<IInputCapture>();


        _service = new GlobalHotkeyService(
            _config, 
            _parser,
            _matcher,
            _modifierTracker,
            _stringBuilder,
            _mouseButtonMapper,
            () => _inputCapture);
    }

    [Fact]
    public void Start_InitializesInputCapture()
    {
        // Act
        _service.Start();

        // Assert
        _inputCapture.Received(1).Configure(true, true);
        Assert.True(_service.IsRunning);
    }

    [Fact]
    public void OnInputReceived_TriggersRecordingEvent_WhenHotkeyMatch()
    {
        // Arrange
        _service.Start();
        bool eventFired = false;
        _service.ToggleRecordingRequested += (s, e) => eventFired = true;

        // Simulate F9 Press (Code 67, Value 1)
        var args = new InputCaptureEventArgs { Type = InputEventType.Key, Code = 67, Value = 1 };
        
        // Use NSubstitute to raise event
        _inputCapture.InputReceived += Raise.Event<EventHandler<InputCaptureEventArgs>>(this, args);

        // Assert
        Assert.True(eventFired);
    }

    [Fact]
    public void OnInputReceived_DoesNotTrigger_WhenNoMatch()
    {
        // Arrange
        _service.Start();
        bool eventFired = false;
        _service.ToggleRecordingRequested += (s, e) => eventFired = true;

        // Simulate F8 Press (Code 66, Value 1)
        var args = new InputCaptureEventArgs { Type = InputEventType.Key, Code = 66, Value = 1 };
        _inputCapture.InputReceived += Raise.Event<EventHandler<InputCaptureEventArgs>>(this, args);

        // Assert
        Assert.False(eventFired);
    }

    [Fact]
    public void OnInputReceived_MouseButtonPress_RaisesRawInputReceived()
    {
        // Arrange
        _service.Start();
        RawHotkeyInputEventArgs? received = null;
        _service.RawInputReceived += (_, e) => received = e;

        // Act
        var args = new InputCaptureEventArgs
        {
            Type = InputEventType.MouseButton,
            Code = InputEventCode.BTN_EXTRA,
            Value = 1
        };
        _inputCapture.InputReceived += Raise.Event<EventHandler<InputCaptureEventArgs>>(this, args);

        // Assert
        Assert.NotNull(received);
        Assert.Equal(InputEventCode.BTN_EXTRA, received!.KeyCode);
        Assert.Equal("Mouse Extra", received.HotkeyString);
    }

    [Fact]
    public void OnInputReceived_MouseButtonRelease_RaisesRawKeyReleased()
    {
        // Arrange
        _service.Start();
        RawHotkeyInputEventArgs? released = null;
        _service.RawKeyReleased += (_, e) => released = e;

        // Act
        var args = new InputCaptureEventArgs
        {
            Type = InputEventType.MouseButton,
            Code = InputEventCode.BTN_EXTRA,
            Value = 0
        };
        _inputCapture.InputReceived += Raise.Event<EventHandler<InputCaptureEventArgs>>(this, args);

        // Assert
        Assert.NotNull(released);
        Assert.Equal(InputEventCode.BTN_EXTRA, released!.KeyCode);
        Assert.Equal(string.Empty, released.HotkeyString);
    }
    
    [Fact]
    public void UpdateHotkeys_Parses_Correctly()
    {
        // Act
        // Set Numpad0 which we mocked to return MainKey 82
        _service.UpdateHotkeys("Numpad0", "F10", "F11");

        // Assert
        // GlobalHotkeyService.RecordingHotkeyCode property uses _recordingHotkey.MainKey
        // _recordingHotkey is set via _parser.Parse("Numpad0")
        Assert.Equal(82, _service.RecordingHotkeyCode);
    }

    [Fact]
    public async Task OnInputCaptureError_ShouldAttemptSingleRestart()
    {
        var firstCapture = Substitute.For<IInputCapture>();
        var secondCapture = Substitute.For<IInputCapture>();
        firstCapture.ProviderName.Returns("first");
        secondCapture.ProviderName.Returns("second");
        firstCapture.StartAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var secondStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        secondCapture.StartAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => secondStarted.TrySetResult(true));

        var factoryCall = 0;
        var restartingService = new GlobalHotkeyService(
            _config,
            _parser,
            _matcher,
            _modifierTracker,
            _stringBuilder,
            _mouseButtonMapper,
            () => ++factoryCall == 1 ? firstCapture : secondCapture);

        restartingService.Start();

        firstCapture.Error += Raise.Event<EventHandler<string>>(this, "simulated capture error");
        await secondStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        firstCapture.Received(1).Stop();
        firstCapture.Received(1).Dispose();
        secondCapture.Received(1).Configure(true, true);
        await secondCapture.Received(1).StartAsync(Arg.Any<CancellationToken>());
    }
}
