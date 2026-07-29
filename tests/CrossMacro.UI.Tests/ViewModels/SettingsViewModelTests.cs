
namespace CrossMacro.UI.Tests.ViewModels;

public sealed class SettingsViewModelTests : IDisposable
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
    private readonly IRuntimeLogLevelService _runtimeLogLevelService;
    private readonly IThemeService _themeService;
    private readonly IRuntimeContext _runtimeContext = new FakeRuntimeContext();
    private readonly HotkeySettings _hotkeySettings;
    private readonly SettingsViewModel _viewModel;

    public SettingsViewModelTests()
    {
        _hotkeyService = Substitute.For<IGlobalHotkeyService>();
        _settingsService = Substitute.For<ISettingsService>();
        _textExpansionService = Substitute.For<ITextExpansionService>();
        _externalUrlOpener = Substitute.For<IExternalUrlOpener>();
        _runtimeLogLevelService = Substitute.For<IRuntimeLogLevelService>();
        _themeService = Substitute.For<IThemeService>();
        _hotkeySettings = new HotkeySettings();
        _ = _themeService.AvailableThemes.Returns(["Classic", "Nord"]);
        _ = _themeService.CurrentTheme.Returns("Classic");
        _ = _themeService
            .TryApplyTheme(Arg.Any<string>(), out Arg.Any<string>())
            .Returns(callInfo =>
            {
                callInfo[1] = string.Empty;
                return true;
            });

        // Setup initial settings
        _ = _settingsService.Current.Returns(new AppSettings { EnableTrayIcon = false, StartMinimized = false, EnableTextExpansion = false, Theme = "Classic" });
        _ = _settingsService.SaveAsync().Returns(Task.CompletedTask);

        _viewModel = new SettingsViewModel(
            _hotkeyService,
            _settingsService,
            _textExpansionService,
            _hotkeySettings,
            _externalUrlOpener,
            _runtimeLogLevelService,
            _themeService,
            _runtimeContext);
    }

    public void Dispose()
    {
        _viewModel.Dispose();
    }

    [Fact]
    public void Construction_InitializesProperties()
    {
        _ = _viewModel.RecordingHotkey.Should().Be("F8"); // Default
        _ = _viewModel.EnableTrayIcon.Should().BeFalse();
        _ = _viewModel.StartMinimized.Should().BeFalse();
        _ = _viewModel.SelectedTheme.Should().Be("Classic");
    }

    [Fact]
    public void Construction_ExposesSuppliedHotkeyAndLocalizationServices()
    {
        var localizationService = Substitute.For<ILocalizationService>();
        _ = localizationService[Arg.Any<string>()].Returns(call => call.Arg<string>());

        var vm = new SettingsViewModel(
            _hotkeyService,
            _settingsService,
            _textExpansionService,
            _hotkeySettings,
            _externalUrlOpener,
            _runtimeLogLevelService,
            _themeService,
            _runtimeContext,
            localizationService);

        _ = vm.GlobalHotkeyService.Should().BeSameAs(_hotkeyService);
        _ = vm.LocalizationService.Should().BeSameAs(localizationService);
    }

    [Fact]
    public void Construction_ExposesAllSupportedLanguages()
    {
        var codes = _viewModel.AvailableLanguages.Select(option => option.Code).ToArray();
        var expectedCodes = SettingsViewModel.SupportedLanguages
            .OrderByDescending(language => language.IsDefault)
            .ThenBy(language => language.EnglishName, StringComparer.Ordinal)
            .Select(language => language.Code)
            .ToArray();

        _ = codes.Should().OnlyHaveUniqueItems();
        _ = codes.Should().Equal(expectedCodes);
        _ = SettingsViewModel.SupportedLanguages.Should().ContainSingle(language => language.IsDefault)
            .Which.Code.Should().Be("en");
        _ = SettingsViewModel.SupportedLanguages.Select(language => language.ResourceKey)
            .Should().OnlyHaveUniqueItems()
            .And.AllSatisfy(resourceKey => resourceKey.Should().StartWith("Language_"));
    }

    [Fact]
    public void SelectedLanguageOption_WhenChanged_UpdatesSettingsAndSaves()
    {
        var localizationService = Substitute.For<ILocalizationService>();
        _ = localizationService[Arg.Any<string>()].Returns(call => call.Arg<string>());
        var vm = new SettingsViewModel(
            _hotkeyService,
            _settingsService,
            _textExpansionService,
            _hotkeySettings,
            _externalUrlOpener,
            _runtimeLogLevelService,
            _themeService,
            _runtimeContext,
            localizationService);

        vm.SelectedLanguageOption = vm.AvailableLanguages.Single(option => option.Code is "ja");

        _ = vm.SelectedLanguage.Should().Be("ja");
        _ = _settingsService.Current.Language.Should().Be("ja");
        localizationService.Received(1).SetCulture("ja");
        _ = _settingsService.Received(1).SaveAsync();
    }

    [Fact]
    public void SelectedLanguageOption_WhenLanguageLabelsRefresh_KeepsSelectedOptionInstance()
    {
        var localizationService = Substitute.For<ILocalizationService>();
        _ = localizationService[Arg.Any<string>()].Returns(call => call.Arg<string>());
        var vm = new SettingsViewModel(
            _hotkeyService,
            _settingsService,
            _textExpansionService,
            _hotkeySettings,
            _externalUrlOpener,
            _runtimeLogLevelService,
            _themeService,
            _runtimeContext,
            localizationService);

        var selectedBefore = vm.AvailableLanguages.Single(option => option.Code is "zh");

        vm.SelectedLanguageOption = selectedBefore;
        _ = localizationService["Language_Chinese"].Returns("中文");
        _ = localizationService["Language_English"].Returns("英语");

        vm.SelectedLanguage = "en";

        _ = vm.AvailableLanguages.Single(option => option.Code is "zh").Should().BeSameAs(selectedBefore);
        _ = vm.SelectedLanguageOption.Should().BeSameAs(vm.AvailableLanguages.Single(option => option.Code is "en"));
    }

    [Fact]
    public void Construction_WhenPersistedLanguageIsUnsupported_FallsBackToEnglish()
    {
        _ = _settingsService.Current.Returns(new AppSettings
        {
            EnableTrayIcon = false,
            StartMinimized = false,
            EnableTextExpansion = false,
            Theme = "Classic",
            Language = "auto",
        });

        var vm = new SettingsViewModel(
            _hotkeyService,
            _settingsService,
            _textExpansionService,
            _hotkeySettings,
            _externalUrlOpener,
            _runtimeLogLevelService,
            _themeService,
            _runtimeContext);

        _ = vm.SelectedLanguage.Should().Be("en");
        _ = vm.SelectedLanguageOption!.Code.Should().Be("en");
    }

    [Fact]
    public void Construction_WhenPersistedLanguageUsesDifferentCase_UsesCanonicalSupportedCode()
    {
        _ = _settingsService.Current.Returns(new AppSettings
        {
            EnableTrayIcon = false,
            StartMinimized = false,
            EnableTextExpansion = false,
            Theme = "Classic",
            Language = "JA",
        });

        var vm = new SettingsViewModel(
            _hotkeyService,
            _settingsService,
            _textExpansionService,
            _hotkeySettings,
            _externalUrlOpener,
            _runtimeLogLevelService,
            _themeService,
            _runtimeContext);

        _ = vm.SelectedLanguage.Should().Be("ja");
        _ = _settingsService.Current.Language.Should().Be("ja");
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
            _runtimeLogLevelService,
            null!,
            _runtimeContext);

        _ = act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RecordingHotkey_WhenChanged_UpdatesSettingsAndService()
    {
        // Act
        _viewModel.RecordingHotkey = "F12";

        // Assert
        _ = _hotkeySettings.RecordingHotkey.Should().Be("F12");
        _ = _viewModel.RecordingHotkey.Should().Be("F12");

        // Since service is not running in test, UpdateHotkeys might catch exception or skip?
        // Code: if (_hotkeyService.IsRunning) UpdateHotkeys...
        // Let's assume IsRunning = false by default mock.
        _hotkeyService.DidNotReceive().UpdateHotkeys(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public void HotkeyChange_WhenServiceRunning_UpdatesHotkeys()
    {
        // Arrange
        _ = _hotkeyService.IsRunning.Returns(returnThis: true);

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
        _viewModel.TrayIconEnabledChanged += (s, val) =>
        {
            eventFired = true;
            _ = val.Should().BeTrue();
        };

        // Act
        _viewModel.EnableTrayIcon = true;

        // Assert
        _ = _settingsService.Current.EnableTrayIcon.Should().BeTrue();
        _ = _settingsService.Received(1).SaveAsync();
        _ = eventFired.Should().BeTrue();
    }

    [Fact]
    public void StartMinimized_WhenChanged_SavesSettingsAndAutoEnablesTray()
    {
        bool trayEventFired = false;
        _viewModel.TrayIconEnabledChanged += (_, enabled) =>
        {
            trayEventFired = true;
            enabled.Should().BeTrue();
        };

        _viewModel.StartMinimized = true;

        _ = _viewModel.StartMinimized.Should().BeTrue();
        _ = _viewModel.EnableTrayIcon.Should().BeTrue();
        _ = _settingsService.Current.StartMinimized.Should().BeTrue();
        _ = _settingsService.Current.EnableTrayIcon.Should().BeTrue();
        _ = _settingsService.Received(1).SaveAsync();
        _ = trayEventFired.Should().BeTrue();
    }

    [Fact]
    public void EnableTrayIcon_WhenDisabledWhileStartMinimizedIsEnabled_DisablesStartMinimizedToo()
    {
        _viewModel.StartMinimized = true;

        _viewModel.EnableTrayIcon = false;

        _ = _viewModel.EnableTrayIcon.Should().BeFalse();
        _ = _viewModel.StartMinimized.Should().BeFalse();
        _ = _settingsService.Current.EnableTrayIcon.Should().BeFalse();
        _ = _settingsService.Current.StartMinimized.Should().BeFalse();
        _ = _settingsService.Received(2).SaveAsync();
    }

    [Fact]
    public void EnableTrayIcon_WhenDisablingWouldSaveInvalidStartupStateAndSaveFails_RollsBackBoth()
    {
        _viewModel.StartMinimized = true;
        _settingsService.ClearReceivedCalls();
        _ = _settingsService.SaveAsync().Returns(Task.FromException(new InvalidOperationException("disk full")));

        _viewModel.EnableTrayIcon = false;

        _ = _viewModel.EnableTrayIcon.Should().BeTrue();
        _ = _viewModel.StartMinimized.Should().BeTrue();
        _ = _settingsService.Current.EnableTrayIcon.Should().BeTrue();
        _ = _settingsService.Current.StartMinimized.Should().BeTrue();
    }

    [Fact]
    public void StartMinimized_WhenSaveFails_RollsBackAndDoesNotEnableTray()
    {
        _ = _settingsService.SaveAsync().Returns(Task.FromException(new InvalidOperationException("disk full")));

        _viewModel.StartMinimized = true;

        _ = _viewModel.StartMinimized.Should().BeFalse();
        _ = _viewModel.EnableTrayIcon.Should().BeFalse();
        _ = _settingsService.Current.StartMinimized.Should().BeFalse();
        _ = _settingsService.Current.EnableTrayIcon.Should().BeFalse();
    }

    [Fact]
    public void StartMinimized_WhenTrayIsUnsupported_SavesWithoutEnablingTray()
    {
        var settings = new AppSettings
        {
            EnableTrayIcon = false,
            StartMinimized = false,
            EnableTextExpansion = false,
            Theme = "Classic",
        };
        _ = _settingsService.Current.Returns(settings);

        var viewModel = new SettingsViewModel(
            _hotkeyService,
            _settingsService,
            _textExpansionService,
            _hotkeySettings,
            _externalUrlOpener,
            _runtimeLogLevelService,
            _themeService,
            new FakeRuntimeContext { IsFlatpak = true });

        _ = viewModel.IsTraySettingsVisible.Should().BeFalse();

        viewModel.StartMinimized = true;

        _ = viewModel.StartMinimized.Should().BeTrue();
        _ = viewModel.EnableTrayIcon.Should().BeFalse();
        _ = _settingsService.Current.StartMinimized.Should().BeTrue();
        _ = _settingsService.Current.EnableTrayIcon.Should().BeFalse();
        _ = _settingsService.Received(1).SaveAsync();
    }

    [Fact]
    public async Task EnableTextExpansion_WhenChanged_SavesSettingsAndTogglesService()
    {
        var startCalled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _textExpansionService.When(x => x.Start()).Do(_ => startCalled.TrySetResult(true));
        _ = _textExpansionService.StopExpansionAsync(Arg.Any<CancellationToken>()).Returns(unusedCallInfo =>
        {
            _ = stopStarted.TrySetResult();
            return stopCompletion.Task;
        });

        // Act - Enable
        _viewModel.EnableTextExpansion = true;

        // Assert - Enable
        _ = _settingsService.Current.EnableTextExpansion.Should().BeTrue();
        _ = _settingsService.Received(1).SaveAsync();
        _ = await startCalled.Task.WaitAsync(TimeSpan.FromSeconds(2));
        _textExpansionService.Received(1).Start();

        // Act - Disable
        _viewModel.EnableTextExpansion = false;

        // Assert - Disable
        _ = _settingsService.Current.EnableTextExpansion.Should().BeFalse();
        await stopStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        TestAssertions.VerifyTask(() => _textExpansionService.Received(1).StopExpansionAsync(Arg.Any<CancellationToken>()));
        stopCompletion.SetResult();
    }

    [Fact]
    public async Task EnableTextExpansion_WhenDisabled_AwaitsAsyncStopCompletion()
    {
        _settingsService.Current.EnableTextExpansion = true;
        var stopStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = _textExpansionService.StopExpansionAsync(Arg.Any<CancellationToken>()).Returns(unusedCallInfo =>
        {
            _ = stopStarted.TrySetResult();
            return stopCompletion.Task;
        });

        var rollbackObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _viewModel.PropertyChanged += (_, args) =>
        {
            if (string.Equals(args.PropertyName, nameof(SettingsViewModel.EnableTextExpansion), StringComparison.Ordinal) &&
                _settingsService.Current.EnableTextExpansion)
            {
                rollbackObserved.TrySetResult();
            }
        };

        _viewModel.EnableTextExpansion = false;
        await stopStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.False(_settingsService.Current.EnableTextExpansion);
        TestAssertions.VerifyTask(() => _textExpansionService.Received(1).StopExpansionAsync(Arg.Any<CancellationToken>()));
        Assert.False(rollbackObserved.Task.IsCompleted);
        stopCompletion.SetException(new InvalidOperationException("stop failed"));
        await rollbackObserved.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(_settingsService.Current.EnableTextExpansion);
        TestAssertions.VerifyTask(() => _textExpansionService.Received(1).StopExpansionAsync(Arg.Any<CancellationToken>()));
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
        _viewModel.SelectedLogLevel = "Warning";

        _ = _settingsService.Current.LogLevel.Should().Be("Warning");
        Received.InOrder(() =>
        {
            _runtimeLogLevelService.SetLogLevel("Warning");
            _ = _settingsService.SaveAsync();
        });
        _ = _settingsService.Received(1).SaveAsync();
    }

    [Fact]
    public void SelectedLogLevel_WhenSaveFails_RollsBackAndRestoresRuntimeLevel()
    {
        _settingsService.Current.LogLevel = "Information";
        _ = _settingsService.SaveAsync().Returns(Task.FromException(new InvalidOperationException("disk full")));

        _viewModel.SelectedLogLevel = "Warning";

        _ = _viewModel.SelectedLogLevel.Should().Be("Information");
        _ = _settingsService.Current.LogLevel.Should().Be("Information");
        Received.InOrder(() =>
        {
            _runtimeLogLevelService.SetLogLevel("Warning");
            _ = _settingsService.SaveAsync();
            _runtimeLogLevelService.SetLogLevel("Information");
        });
    }

    [Fact]
    public void CheckForUpdates_WhenChanged_UpdatesSettingsAndSaves()
    {
        // Arrange
        _settingsService.Current.CheckForUpdates = true;

        // Act
        _viewModel.CheckForUpdates = false;

        // Assert
        _ = _settingsService.Current.CheckForUpdates.Should().BeFalse();
        _ = _settingsService.Received(1).SaveAsync();
    }

    [Fact]
    public void SelectedTheme_WhenChanged_UpdatesSettingsAndSaves()
    {
        // Act
        _viewModel.SelectedTheme = "Nord";

        // Assert
        _ = _settingsService.Current.Theme.Should().Be("Nord");
        _ = _themeService.Received(1).TryApplyTheme("Nord", out Arg.Any<string>());
        _ = _settingsService.Received(1).SaveAsync();
    }

    [Fact]
    public void SelectedTheme_WhenApplyFails_RevertsToCurrentTheme()
    {
        _ = _themeService.CurrentTheme.Returns("Classic");
        _ = _themeService
            .TryApplyTheme("Broken", out Arg.Any<string>())
            .Returns(callInfo =>
            {
                callInfo[1] = "Unknown theme";
                return false;
            });

        _viewModel.SelectedTheme = "Broken";

        _ = _viewModel.SelectedTheme.Should().Be("Classic");
        _ = _settingsService.Current.Theme.Should().Be("Classic");
        _ = _settingsService.DidNotReceive().SaveAsync();
    }

    [Fact]
    public void EnableTextExpansion_WhenSaveFails_RollsBackAndDoesNotToggleService()
    {
        _ = _settingsService.SaveAsync().Returns(Task.FromException(new InvalidOperationException("disk full")));

        _viewModel.EnableTextExpansion = true;

        _ = _settingsService.Current.EnableTextExpansion.Should().BeFalse();
        _textExpansionService.DidNotReceive().Start();
        _textExpansionService.DidNotReceive().StopExpansion();
    }

    [Fact]
    public async Task StaleFailedSave_DoesNotRollbackNewerSettingChange()
    {
        var firstSave = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondSave = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = _settingsService.SaveAsync().Returns(firstSave.Task, secondSave.Task);

        _viewModel.EnableTextExpansion = true;
        _viewModel.CheckForUpdates = true;

        firstSave.SetException(new InvalidOperationException("disk full"));
        await Task.Yield();
        secondSave.SetResult(true);
        await Task.Delay(25);

        _ = _viewModel.EnableTextExpansion.Should().BeTrue();
        _ = _viewModel.CheckForUpdates.Should().BeTrue();
    }

    [Fact]
    public async Task StaleSuccessfulSave_ReconcilesTheLatestTextExpansionState()
    {
        var firstSave = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondSave = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = _settingsService.SaveAsync().Returns(firstSave.Task, secondSave.Task);

        _viewModel.EnableTextExpansion = true;
        _viewModel.CheckForUpdates = true;

        firstSave.SetResult(true);
        await Task.Yield();

        _textExpansionService.Received(1).Start();

        secondSave.SetResult(true);
    }

    [Fact]
    public async Task ProfileCommands_WhenManagerProvided_ManageProfilesAndRefreshSelection()
    {
        var defaultProfile = new ProfileInfo { Id = "default", Name = "Default" };
        var workProfile = new ProfileInfo { Id = "work", Name = "Work" };
        var profiles = new List<ProfileInfo> { defaultProfile };
        var profileManager = Substitute.For<IProfileManager>();
        _ = profileManager.Profiles.Returns(_ => profiles.ToArray());
        _ = profileManager.ActiveProfile.Returns(_ => defaultProfile);
        _ = profileManager.CreateProfileAsync("Work").Returns(_ =>
        {
            profiles.Add(workProfile);
            return Task.FromResult(workProfile);
        });

        var vm = new SettingsViewModel(
            _hotkeyService,
            _settingsService,
            _textExpansionService,
            _hotkeySettings,
            _externalUrlOpener,
            _runtimeLogLevelService,
            _themeService,
            _runtimeContext,
            profileManager: profileManager);

        _ = vm.AvailableProfiles.Should().ContainSingle().Which.Name.Should().Be("Default");
        _ = vm.SelectedProfile.Should().Be(defaultProfile);

        vm.NewProfileName = " Work ";
        await vm.CreateProfileAsync();

        _ = await profileManager.Received(1).CreateProfileAsync("Work");
        _ = vm.AvailableProfiles.Select(profile => profile.Name).Should().Equal("Default", "Work");
        _ = vm.SelectedProfile.Should().Be(workProfile);
        _ = vm.NewProfileName.Should().BeEmpty();

        vm.NewProfileName = "Renamed Work";
        await vm.RenameSelectedProfileAsync();

        await profileManager.Received(1).RenameProfileAsync("work", "Renamed Work");
        _ = vm.NewProfileName.Should().BeEmpty();

        await vm.SwitchProfileAsync();

        await profileManager.Received(1).SwitchProfileAsync("work");

        await vm.DeleteSelectedProfileAsync();

        await profileManager.Received(1).DeleteProfileAsync("work");
    }

    [Fact]
    public async Task DeleteSelectedProfileAsync_WhenConfirmationDeclined_DoesNotDeleteProfile()
    {
        var defaultProfile = new ProfileInfo { Id = "default", Name = "Default" };
        var workProfile = new ProfileInfo { Id = "work", Name = "Work" };
        var profileManager = Substitute.For<IProfileManager>();
        _ = profileManager.Profiles.Returns([defaultProfile, workProfile]);
        _ = profileManager.ActiveProfile.Returns(defaultProfile);
        var dialogService = Substitute.For<IDialogService>();
        var localizationService = CreateProfileLocalizationService();
        _ = dialogService.ShowConfirmationAsync("Delete Profile", "Delete profile 'Work'?", "Yes", "No")
            .Returns(returnThis: false);

        var vm = new SettingsViewModel(
            _hotkeyService,
            _settingsService,
            _textExpansionService,
            _hotkeySettings,
            _externalUrlOpener,
            _runtimeLogLevelService,
            _themeService,
            _runtimeContext,
            localizationService: localizationService,
            profileManager: profileManager,
            dialogService: dialogService)
        {
            SelectedProfile = workProfile,
        };

        await vm.DeleteSelectedProfileAsync();

        _ = await dialogService.Received(1).ShowConfirmationAsync("Delete Profile", "Delete profile 'Work'?", "Yes", "No");
        await profileManager.DidNotReceive().DeleteProfileAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task DeleteSelectedProfileAsync_WhenConfirmationAccepted_DeletesProfile()
    {
        var defaultProfile = new ProfileInfo { Id = "default", Name = "Default" };
        var workProfile = new ProfileInfo { Id = "work", Name = "Work" };
        var profiles = new List<ProfileInfo> { defaultProfile, workProfile };
        var profileManager = Substitute.For<IProfileManager>();
        _ = profileManager.Profiles.Returns(_ => profiles.ToArray());
        _ = profileManager.ActiveProfile.Returns(defaultProfile);
        _ = profileManager.DeleteProfileAsync("work").Returns(unusedCallInfo =>
        {
            _ = profiles.Remove(workProfile);
            return Task.CompletedTask;
        });
        var dialogService = Substitute.For<IDialogService>();
        var localizationService = CreateProfileLocalizationService();
        _ = dialogService.ShowConfirmationAsync("Delete Profile", "Delete profile 'Work'?", "Yes", "No")
            .Returns(returnThis: true);

        var vm = new SettingsViewModel(
            _hotkeyService,
            _settingsService,
            _textExpansionService,
            _hotkeySettings,
            _externalUrlOpener,
            _runtimeLogLevelService,
            _themeService,
            _runtimeContext,
            localizationService: localizationService,
            profileManager: profileManager,
            dialogService: dialogService)
        {
            SelectedProfile = workProfile,
        };

        await vm.DeleteSelectedProfileAsync();

        await profileManager.Received(1).DeleteProfileAsync("work");
        _ = vm.AvailableProfiles.Should().ContainSingle().Which.Should().Be(defaultProfile);
        _ = vm.SelectedProfile.Should().Be(defaultProfile);
    }

    [Fact]
    public void Dispose_UnsubscribesFromProfileChanged()
    {
        var defaultProfile = new ProfileInfo { Id = "default", Name = "Default" };
        var workProfile = new ProfileInfo { Id = "work", Name = "Work" };
        var profileManager = Substitute.For<IProfileManager>();
        _ = profileManager.Profiles.Returns([defaultProfile, workProfile]);
        _ = profileManager.ActiveProfile.Returns(defaultProfile);

        var vm = new SettingsViewModel(
            _hotkeyService,
            _settingsService,
            _textExpansionService,
            _hotkeySettings,
            _externalUrlOpener,
            _runtimeLogLevelService,
            _themeService,
            _runtimeContext,
            profileManager: profileManager);

        vm.Dispose();
        _ = profileManager.ActiveProfile.Returns(workProfile);
        profileManager.ProfileChanged += Raise.Event<EventHandler<ProfileChangedEventArgs>>(profileManager, new ProfileChangedEventArgs(workProfile));

        _ = vm.SelectedProfile.Should().Be(defaultProfile);
    }

    [Fact]
    public async Task ProfileOperation_WhenManagerRejects_RaisesFailureAndResetsProgress()
    {
        var defaultProfile = new ProfileInfo { Id = "default", Name = "Default" };
        var profileManager = Substitute.For<IProfileManager>();
        _ = profileManager.Profiles.Returns([defaultProfile]);
        _ = profileManager.ActiveProfile.Returns(defaultProfile);
        _ = profileManager.DeleteProfileAsync("default").Returns<Task>(_ => throw new InvalidOperationException("Cannot delete default profile."));

        var vm = new SettingsViewModel(
            _hotkeyService,
            _settingsService,
            _textExpansionService,
            _hotkeySettings,
            _externalUrlOpener,
            _runtimeLogLevelService,
            _themeService,
            _runtimeContext,
            profileManager: profileManager);
        string? failureMessage = null;
        vm.ProfileOperationFailed += (_, message) => failureMessage = message;

        await vm.DeleteSelectedProfileAsync();

        _ = failureMessage.Should().Be("Failed to delete profile: Cannot delete default profile.");
        _ = vm.IsProfileOperationInProgress.Should().BeFalse();
    }

    [Fact]
    public void ProfileChanged_RefreshesHotkeysAndProfileSpecificSettingsOnly()
    {
        var defaultProfile = new ProfileInfo { Id = "default", Name = "Default" };
        var workProfile = new ProfileInfo { Id = "work", Name = "Work" };
        var profileManager = Substitute.For<IProfileManager>();
        _ = profileManager.Profiles.Returns([defaultProfile, workProfile]);
        _ = profileManager.ActiveProfile.Returns(_ => workProfile);

        _settingsService.Current.EnableTextExpansion = true;
        _settingsService.Current.CheckForUpdates = true;
        _settingsService.Current.Theme = "Classic";
        _hotkeySettings.RecordingHotkey = "Ctrl+Alt+R";
        _hotkeySettings.PlaybackHotkey = "Ctrl+Alt+P";
        _hotkeySettings.PauseHotkey = "Ctrl+Alt+Space";

        var vm = new SettingsViewModel(
            _hotkeyService,
            _settingsService,
            _textExpansionService,
            _hotkeySettings,
            _externalUrlOpener,
            _runtimeLogLevelService,
            _themeService,
            _runtimeContext,
            profileManager: profileManager);

        profileManager.ProfileChanged += Raise.Event<EventHandler<ProfileChangedEventArgs>>(profileManager, new ProfileChangedEventArgs(workProfile));

        _ = vm.SelectedProfile.Should().Be(workProfile);
        _ = vm.RecordingHotkey.Should().Be("Ctrl+Alt+R");
        _ = vm.PlaybackHotkey.Should().Be("Ctrl+Alt+P");
        _ = vm.PauseHotkey.Should().Be("Ctrl+Alt+Space");
        _ = vm.EnableTextExpansion.Should().BeTrue();
        _ = vm.CheckForUpdates.Should().BeTrue();
        _ = vm.SelectedTheme.Should().Be("Classic");
    }

    [Fact]
    public void OpenGitHub_UsesExternalUrlOpener()
    {
        // Act
        _viewModel.OpenGitHub();

        // Assert
        TestAssertions.VerifyTask(() => _externalUrlOpener.Received(1).OpenAsync(new Uri("https://github.com/alper-han/CrossMacro", UriKind.Absolute)));
    }

    private static ILocalizationService CreateProfileLocalizationService()
    {
        var localizationService = Substitute.For<ILocalizationService>();
        _ = localizationService.CurrentCulture.Returns(CultureInfo.InvariantCulture);
        _ = localizationService[Arg.Any<string>()].Returns(call => call.Arg<string>());
        _ = localizationService["Settings_ProfileDeleteTitle"].Returns("Delete Profile");
        _ = localizationService["Settings_ProfileDeleteMessage"].Returns("Delete profile '{0}'?");
        _ = localizationService["Settings_ProfileDeleteFailed"].Returns("Failed to delete profile");
        return localizationService;
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
            _runtimeLogLevelService,
            _themeService,
            runtimeContext);

        _ = vm.IsUpdateSettingsVisible.Should().BeFalse();
        _ = vm.IsTraySettingsVisible.Should().BeFalse();
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
            _runtimeLogLevelService,
            _themeService,
            runtimeContext);

        _ = vm.IsUpdateSettingsVisible.Should().BeTrue();
        _ = vm.IsTraySettingsVisible.Should().BeTrue();
    }
}
