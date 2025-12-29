using System;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services;
using CrossMacro.UI.ViewModels;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CrossMacro.UI.Tests.ViewModels;

public class SettingsViewModelTests
{
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly ISettingsService _settingsService;
    private readonly ITextExpansionService _textExpansionService;
    private readonly HotkeySettings _hotkeySettings;
    private readonly SettingsViewModel _viewModel;

    public SettingsViewModelTests()
    {
        _hotkeyService = Substitute.For<IGlobalHotkeyService>();
        _settingsService = Substitute.For<ISettingsService>();
        _textExpansionService = Substitute.For<ITextExpansionService>();
        _hotkeySettings = new HotkeySettings();
        
        // Setup initial settings
        _settingsService.Current.Returns(new AppSettings { EnableTrayIcon = false, EnableTextExpansion = false });

        _viewModel = new SettingsViewModel(
            _hotkeyService, 
            _settingsService, 
            _textExpansionService,
            _hotkeySettings);
    }

    [Fact]
    public void Construction_InitializesProperties()
    {
        _viewModel.RecordingHotkey.Should().Be("F8"); // Default
        _viewModel.EnableTrayIcon.Should().BeFalse();
    }

    [Fact]
    public void RecordingHotkey_WhenChanged_UpdatesSettingsAndService()
    {
        // Act
        _viewModel.RecordingHotkey = "F12";

        // Assert
        _hotkeySettings.RecordingHotkey.Should().Be("F12");
        _viewModel.RecordingHotkey.Should().Be("F12");
        
        // Since service is not running in test, UpdateHotkeys might catch exception or skip?
        // Code: if (_hotkeyService.IsRunning) UpdateHotkeys...
        // Let's assume IsRunning = false by default mock.
        _hotkeyService.DidNotReceive().UpdateHotkeys(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public void HotkeyChange_WhenServiceRunning_UpdatesHotkeys()
    {
        // Arrange
        _hotkeyService.IsRunning.Returns(true);

        // Act
        _viewModel.PlaybackHotkey = "Ctrl+P";

        // Assert
        _hotkeyService.Received(1).UpdateHotkeys("F8", "Ctrl+P", "F10");
    }

    [Fact]
    public void EnableTrayIcon_WhenChanged_SavesSettingsAndFiresEvent()
    {
        // Arrange
        bool eventFired = false;
        _viewModel.TrayIconEnabledChanged += (s, val) => {
            eventFired = true;
            val.Should().BeTrue();
        };

        // Act
        _viewModel.EnableTrayIcon = true;

        // Assert
        _settingsService.Current.EnableTrayIcon.Should().BeTrue();
        _settingsService.Received(1).SaveAsync();
        eventFired.Should().BeTrue();
    }

    [Fact]
    public void EnableTextExpansion_WhenChanged_SavesSettingsAndTogglesService()
    {
        // Act - Enable
        _viewModel.EnableTextExpansion = true;

        // Assert - Enable
        _settingsService.Current.EnableTextExpansion.Should().BeTrue();
        _settingsService.Received(1).SaveAsync();
        
        // Wait for async task
        System.Threading.Thread.Sleep(50);
        _textExpansionService.Received(1).Start();

        // Act - Disable
        _viewModel.EnableTextExpansion = false;
        
        // Assert - Disable
        _settingsService.Current.EnableTextExpansion.Should().BeFalse();
        System.Threading.Thread.Sleep(50);
        _textExpansionService.Received(1).Stop();
    }

    [Fact]
    public void StartHotkeyService_CallsServiceStart()
    {
        // Act
        _viewModel.StartHotkeyService();

        // Assert
        _hotkeyService.Received(1).Start();
    }
}
