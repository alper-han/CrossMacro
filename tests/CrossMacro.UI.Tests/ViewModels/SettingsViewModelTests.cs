using System;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services;
using CrossMacro.UI.Services;
using CrossMacro.UI.ViewModels;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CrossMacro.UI.Tests.ViewModels;

public class SettingsViewModelTests
{
    private sealed class FakeRuntimeContext : IRuntimeContext
    {
        public bool IsLinux => true;
        public bool IsWindows => false;
        public bool IsMacOS => false;
        public bool IsFlatpak { get; set; }
        public string? SessionType => "wayland";
    }

    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly ISettingsService _settingsService;
    private readonly ITextExpansionService _textExpansionService;
    private readonly IExternalUrlOpener _externalUrlOpener;
    private readonly IThemeService _themeService;
    private readonly HotkeySettings _hotkeySettings;
    private readonly SettingsViewModel _viewModel;

    public SettingsViewModelTests()
    {
        _hotkeyService = Substitute.For<IGlobalHotkeyService>();
        _settingsService = Substitute.For<ISettingsService>();
        _textExpansionService = Substitute.For<ITextExpansionService>();
        _externalUrlOpener = Substitute.For<IExternalUrlOpener>();
        _themeService = Substitute.For<IThemeService>();
        _hotkeySettings = new HotkeySettings();
        _themeService.AvailableThemes.Returns(new[] { "Classic", "Nord" });
        _themeService.CurrentTheme.Returns("Classic");
        _themeService
            .TryApplyTheme(Arg.Any<string>(), out Arg.Any<string>())
            .Returns(callInfo =>
            {
                callInfo[1] = string.Empty;
                return true;
            });
        
        // Setup initial settings
        _settingsService.Current.Returns(new AppSettings { EnableTrayIcon = false, EnableTextExpansion = false, Theme = "Classic" });

        _viewModel = new SettingsViewModel(
            _hotkeyService, 
            _settingsService, 
            _textExpansionService,
            _hotkeySettings,
            _externalUrlOpener,
            _themeService);
    }

    [Fact]
    public void Construction_InitializesProperties()
    {
        _viewModel.RecordingHotkey.Should().Be("F8"); // Default
        _viewModel.EnableTrayIcon.Should().BeFalse();
        _viewModel.SelectedTheme.Should().Be("Classic");
    }

    [Fact]
    public void Construction_WhenThemeServiceIsNull_Throws()
    {
        var act = () => new SettingsViewModel(
            _hotkeyService,
            _settingsService,
            _textExpansionService,
            _hotkeySettings,
            _externalUrlOpener,
            null!);

        act.Should().Throw<ArgumentNullException>();
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
        _settingsService.Received(1).Save();
        eventFired.Should().BeTrue();
    }

    [Fact]
    public async Task EnableTextExpansion_WhenChanged_SavesSettingsAndTogglesService()
    {
        var startCalled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopCalled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _textExpansionService.When(x => x.Start()).Do(_ => startCalled.TrySetResult(true));
        _textExpansionService.When(x => x.Stop()).Do(_ => stopCalled.TrySetResult(true));

        // Act - Enable
        _viewModel.EnableTextExpansion = true;

        // Assert - Enable
        _settingsService.Current.EnableTextExpansion.Should().BeTrue();
        _settingsService.Received(1).Save();
        await startCalled.Task.WaitAsync(TimeSpan.FromSeconds(2));
        _textExpansionService.Received(1).Start();

        // Act - Disable
        _viewModel.EnableTextExpansion = false;
        
        // Assert - Disable
        _settingsService.Current.EnableTextExpansion.Should().BeFalse();
        await stopCalled.Task.WaitAsync(TimeSpan.FromSeconds(2));
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

    [Fact]
    public void SelectedLogLevel_WhenChanged_UpdatesSettingsAndSaves()
    {
        // Act
        _viewModel.SelectedLogLevel = "Warning";

        // Assert
        _settingsService.Current.LogLevel.Should().Be("Warning");
        _settingsService.Received(1).Save();
    }

    [Fact]
    public void CheckForUpdates_WhenChanged_UpdatesSettingsAndSaves()
    {
        // Arrange
        _settingsService.Current.CheckForUpdates = true;

        // Act
        _viewModel.CheckForUpdates = false;

        // Assert
        _settingsService.Current.CheckForUpdates.Should().BeFalse();
        _settingsService.Received(1).Save();
    }

    [Fact]
    public void SelectedTheme_WhenChanged_UpdatesSettingsAndSaves()
    {
        // Act
        _viewModel.SelectedTheme = "Nord";

        // Assert
        _settingsService.Current.Theme.Should().Be("Nord");
        _themeService.Received(1).TryApplyTheme("Nord", out Arg.Any<string>());
        _settingsService.Received(1).Save();
    }

    [Fact]
    public void SelectedTheme_WhenApplyFails_RevertsToCurrentTheme()
    {
        _themeService.CurrentTheme.Returns("Classic");
        _themeService
            .TryApplyTheme("Broken", out Arg.Any<string>())
            .Returns(callInfo =>
            {
                callInfo[1] = "Unknown theme";
                return false;
            });

        _viewModel.SelectedTheme = "Broken";

        _viewModel.SelectedTheme.Should().Be("Classic");
        _settingsService.Current.Theme.Should().Be("Classic");
        _settingsService.DidNotReceive().Save();
    }

    [Fact]
    public void EnableTextExpansion_WhenSaveFails_RollsBackAndDoesNotToggleService()
    {
        _settingsService.When(x => x.Save()).Do(_ => throw new InvalidOperationException("disk full"));

        _viewModel.EnableTextExpansion = true;

        _settingsService.Current.EnableTextExpansion.Should().BeFalse();
        _textExpansionService.DidNotReceive().Start();
        _textExpansionService.DidNotReceive().Stop();
    }

    [Fact]
    public void OpenGitHub_UsesExternalUrlOpener()
    {
        // Act
        _viewModel.OpenGitHub();

        // Assert
        _externalUrlOpener.Received(1).Open("https://github.com/alper-han/CrossMacro");
    }

    [Fact]
    public void Construction_WhenFlatpakRuntime_HidesUpdateAndTraySettings()
    {
        var runtimeContext = new FakeRuntimeContext { IsFlatpak = true };
        var vm = new SettingsViewModel(
            _hotkeyService,
            _settingsService,
            _textExpansionService,
            _hotkeySettings,
            _externalUrlOpener,
            _themeService,
            runtimeContext);

        vm.IsUpdateSettingsVisible.Should().BeFalse();
        vm.IsTraySettingsVisible.Should().BeFalse();
    }

    [Fact]
    public void Construction_WhenNonFlatpakRuntime_ShowsUpdateAndTraySettings()
    {
        var runtimeContext = new FakeRuntimeContext { IsFlatpak = false };
        var vm = new SettingsViewModel(
            _hotkeyService,
            _settingsService,
            _textExpansionService,
            _hotkeySettings,
            _externalUrlOpener,
            _themeService,
            runtimeContext);

        vm.IsUpdateSettingsVisible.Should().BeTrue();
        vm.IsTraySettingsVisible.Should().BeTrue();
    }
}
