namespace CrossMacro.UI.Tests.Services;

public sealed class DesktopStartupInitializationServiceTests
{
    [Fact]
    public async Task InitializeAsync_RestoresLoadedMacroSessionAfterProfileStartup()
    {
        var settingsService = Substitute.For<ISettingsService>();
        var themeService = Substitute.For<IThemeService>();
        var localizationService = new LocalizationService();
        var profileManager = Substitute.For<IProfileManager>();
        var profileRuntimeState = Substitute.For<IProfileRuntimeState>();
        var loadedMacroSession = new LoadedMacroSession(Substitute.For<ILocalizationService>());
        var store = Substitute.For<IProfileLoadedMacroSessionStore>();
        var macroFileManager = Substitute.For<IMacroFileManager>();
        var profileDirectory = "/profiles/default";
        var sessionId = Guid.NewGuid();
        var snapshot = new LoadedMacroSessionSnapshot(
        [
            new LoadedMacroSessionItemSnapshot(
                sessionId,
                new MacroSequence { Name = "Restored", Events = { new MacroEvent { Type = EventType.Click } } },
                null,
                1),
        ],
        sessionId,
        PlaybackMode: (int)LoadedMacroPlaybackMode.SelectedOnly);
        _ = settingsService.Current.Returns(new AppSettings());
        _ = themeService.TryApplyTheme(Arg.Any<string>(), out Arg.Any<string?>())
            .Returns(callInfo =>
            {
                callInfo[1] = string.Empty;
                return true;
            });
        _ = profileManager.ActiveProfile.Returns(new ProfileInfo { Id = "default" });
        _ = profileManager.GetProfileDirectory("default").Returns(profileDirectory);
        _ = profileRuntimeState.IsInitialized.Returns(true);
        _ = store.LoadAsync(profileDirectory, CancellationToken.None).Returns(snapshot);
        await using var persistenceService = new ProfileLoadedMacroSessionPersistenceService(loadedMacroSession, store, macroFileManager);
        var service = new DesktopStartupInitializationService(
            () => settingsService,
            () => themeService,
            () => localizationService,
            () => new EditorActionDisplayFormatter(localizationService),
            profileManager,
            GuiStartupOptions.Default,
            profileRuntimeState,
            persistenceService);

        await service.InitializeAsync();

        await profileManager.Received(1).InitializeAsync();
        await store.Received(1).LoadAsync(profileDirectory, CancellationToken.None);
        _ = loadedMacroSession.SelectedMacroItem!.SessionId.Should().Be(sessionId);
    }
}
