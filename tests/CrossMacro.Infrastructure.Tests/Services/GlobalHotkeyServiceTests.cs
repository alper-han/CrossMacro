using System;
using System.Collections.Generic;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace CrossMacro.Infrastructure.Tests.Services;

public class GlobalHotkeyServiceTests
{
    private readonly IHotkeyConfigurationService _config;
    private readonly IKeyboardLayoutService _layout;
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

        _layout = Substitute.For<IKeyboardLayoutService>();
        // Mock Key Code resolution
        _layout.GetKeyCode("F9").Returns(67); // 59 + 8 = 67
        _layout.GetKeyCode("F10").Returns(68);
        _layout.GetKeyCode("F11").Returns(69);

        _inputCapture = Substitute.For<IInputCapture>();
        _inputCapture.GetAvailableDevices().Returns(new List<InputDeviceInfo>
        {
            new InputDeviceInfo { IsKeyboard = true, Name = "Test Keyboard", Path = "/dev/input/event0" }
        });

        _service = new GlobalHotkeyService(
            _config, 
            _layout, 
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
    public void UpdateHotkeys_ParsesNumpadKeys_Correctly()
    {
        // Arrange
        // Mock layout service to return -1 (not found) to ensure GlobalHotkeyService's internal logic is used
        _layout.GetKeyCode(Arg.Any<string>()).Returns(-1);

        // Act
        // Set Numpad0 (which maps to 82 in our internal list)
        _service.UpdateHotkeys("Numpad0", "F10", "F11");

        // Assert
        Assert.Equal(82, _service.RecordingHotkeyCode);
    }
}
