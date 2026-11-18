namespace CrossMacro.Infrastructure.Tests.Services.Settings;


public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _tempPath;

    private string DefaultPersistedProfileSettingsPath => Path.Combine(
        _tempPath,
        ConfigFileNames.ProfilesDirectory,
        "default",
        ConfigFileNames.Settings);

    public SettingsServiceTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), "CrossMacroSettingsTests_" + Guid.NewGuid());
        _ = Directory.CreateDirectory(_tempPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempPath))
        {
            Directory.Delete(_tempPath, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_MapsOwnedSnapshotEvenWhenCurrentChangesDuringMapping()
    {
        SettingsService? service = null;
        using var owned = new SettingsService(_tempPath, snapshot =>
        {
            _ = service!.AccessCurrent(settings => settings.PlaybackSpeed = 9);
            return CrossMacro.Infrastructure.Persistence.Settings.SettingsPersistenceMapper.ToProfile(snapshot);
        });
        service = owned;
        _ = await owned.LoadAsync();
        _ = owned.AccessCurrent(settings => settings.PlaybackSpeed = 2);
        await owned.SaveAsync();
        var json = await File.ReadAllTextAsync(DefaultPersistedProfileSettingsPath, TestContext.Current.CancellationToken);
        Assert.Equal(2, JsonSerializer.Deserialize(json, CrossMacroJsonContext.Default.PersistedProfileSettings)?.PlaybackSpeed);
        Assert.Equal(9, owned.Current.PlaybackSpeed);
    }

    [Fact]
    public void Dispose_CanBeCalledRepeatedly()
    {
        using var service = new SettingsService(_tempPath);

        var act = () =>
        {
            service.Dispose();
            service.Dispose();
        };

        _ = act.Should().NotThrow();
    }

    [Fact]
    public async Task LoadAsync_WhenNothingIsPersisted_PublishesDefaultsAsCurrent()
    {
        using var service = new SettingsService(_tempPath);

        var result = await service.LoadAsync();

        _ = result.Should().BeSameAs(service.Current);
        _ = result.PlaybackSpeed.Should().Be(1.0);
        _ = result.Theme.Should().Be(new AppSettings().Theme);
    }

    [Fact]
    public async Task EnsureLoadedAsync_PreservesCurrentIdentityAndPendingChanges()
    {
        using var service = new SettingsService(_tempPath);
        var initial = await service.EnsureLoadedAsync(TestContext.Current.CancellationToken);
        initial.PlaybackSpeed = 2.5;

        var current = await service.EnsureLoadedAsync(TestContext.Current.CancellationToken);

        _ = current.Should().BeSameAs(initial);
        _ = current.PlaybackSpeed.Should().Be(2.5);
        var reloaded = await service.LoadAsync();
        _ = reloaded.Should().NotBeSameAs(initial);
        _ = reloaded.PlaybackSpeed.Should().Be(1.0);
    }

    [Fact]
    public async Task EnsureLoadedAsync_ConcurrentInitializationPublishesOneInstance()
    {
        using var service = new SettingsService(_tempPath);

        var results = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => service.EnsureLoadedAsync(TestContext.Current.CancellationToken)));

        foreach (var settings in results)
        {
            _ = settings.Should().BeSameAs(service.Current);
        }
    }

    [Fact]
    public async Task EnsureLoadedAsync_CanceledInitializationDoesNotCreateFiles()
    {
        using var service = new SettingsService(_tempPath);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var action = () => service.EnsureLoadedAsync(cancellation.Token);

        _ = await action.Should().ThrowAsync<OperationCanceledException>();
        _ = Directory.EnumerateFiles(_tempPath, "*", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Fact]
    public async Task ReloadAsync_FirstLoadCombinesGlobalAndPersistedProfileSettings()
    {
        var profileDirectory = Path.Combine(_tempPath, "profiles", "work");
        _ = Directory.CreateDirectory(profileDirectory);

        await File.WriteAllTextAsync(
            Path.Combine(_tempPath, ConfigFileNames.GlobalSettings),
            JsonSerializer.Serialize(
                new PersistedGlobalSettings
                {
                    EnableTrayIcon = true,
                    Language = "tr",
                },
                CrossMacroJsonContext.Default.PersistedGlobalSettings),
            TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(profileDirectory, ConfigFileNames.Settings),
            JsonSerializer.Serialize(
                new PersistedProfileSettings
                {
                    PlaybackSpeed = 2.5,
                    IsLooping = true,
                },
                CrossMacroJsonContext.Default.PersistedProfileSettings),
            TestContext.Current.CancellationToken);

        using var service = new SettingsService(_tempPath);

        await service.ReloadAsync(profileDirectory);

        _ = service.Current.EnableTrayIcon.Should().BeTrue();
        _ = service.Current.Language.Should().Be("tr");
        _ = service.Current.PlaybackSpeed.Should().Be(2.5);
        _ = service.Current.IsLooping.Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_DoesNotThrow()
    {
        using var service = new SettingsService(_tempPath);
        _ = await service.LoadAsync();
        service.Current.PlaybackSpeed = 1.5;

        var act = async () => await service.SaveAsync();

        _ = await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SaveAndLoadAsync_RoundTripsImmutableMcpSecurityRoots()
    {
        using var service = new SettingsService(_tempPath);
        _ = await service.LoadAsync();
        service.Current.McpSecurity.AllowClipboardRead = true;
        service.Current.McpSecurity.Paths = service.Current.McpSecurity.Paths.WithRoots(
            McpPathSetting.MacroRead,
            [_tempPath]);

        await service.SaveAsync();

        using var reloadedService = new SettingsService(_tempPath);
        var loaded = await reloadedService.LoadAsync();

        _ = loaded.McpSecurity.AllowClipboardRead.Should().BeTrue();
        _ = loaded.McpSecurity.Paths.MacroReadRoots.Should().Equal(_tempPath);
    }

    [Fact]
    public async Task LoadAsync_KeepsStoredMcpSecurityChoicesAndAppliesSafeDefaultsToAbsentKeys()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_tempPath, ConfigFileNames.GlobalSettings),
            """{"mcpSecurity":{"allowShellExecute":true,"allowScreenRead":false}}""",
            TestContext.Current.CancellationToken);

        using var service = new SettingsService(_tempPath);
        var loaded = await service.LoadAsync();

        _ = loaded.McpSecurity.AllowShellExecute.Should().BeTrue();
        _ = loaded.McpSecurity.AllowScreenRead.Should().BeFalse();
        _ = loaded.McpSecurity.AllowCommandExecute.Should().BeFalse();
        _ = loaded.McpSecurity.AllowFileWrite.Should().BeFalse();
        _ = loaded.McpSecurity.AllowSettingsWrite.Should().BeFalse();
        _ = loaded.McpSecurity.AllowMacroRead.Should().BeTrue();
    }

    [Fact]
    public async Task SaveAfterIdleAsync_CoalescesRequests_AndPersistsLatestSettings()
    {
        using var service = new SettingsService(_tempPath);
        _ = await service.LoadAsync();

        service.Current.PlaybackSpeed = 2.0;
        var firstSave = service.SaveAfterIdleAsync();
        service.Current.PlaybackSpeed = 3.0;
        var secondSave = service.SaveAfterIdleAsync();

        _ = secondSave.Should().BeSameAs(firstSave);

        await service.FlushPendingSaveAsync(TestContext.Current.CancellationToken);
        await firstSave;

        using var reloadedService = new SettingsService(_tempPath);
        var loaded = await reloadedService.LoadAsync();

        _ = loaded.PlaybackSpeed.Should().Be(3.0);
    }

    [Fact]
    public async Task SaveAsync_DoesNotPersistRemovedPortalRestoreFields()
    {
        var globalSettingsPath = Path.Combine(_tempPath, ConfigFileNames.GlobalSettings);
        await File.WriteAllTextAsync(
            globalSettingsPath,
            """
            {"theme":"Nord","portalScreenCastRestoreToken":"legacy-token","portalScreenCastRestoreData":"legacy-data"}
            """,
            TestContext.Current.CancellationToken);

        using var service = new SettingsService(_tempPath);
        _ = await service.LoadAsync();
        await service.SaveAsync();

        var persisted = await File.ReadAllTextAsync(globalSettingsPath, TestContext.Current.CancellationToken);
        Assert.DoesNotContain("portalScreenCastRestore", persisted, StringComparison.Ordinal);
        Assert.Contains("\"theme\": \"Nord\"", persisted, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_LegacyThemeWithoutThemeMode_PreservesThemeAsManual()
    {
        var globalSettingsPath = Path.Combine(_tempPath, ConfigFileNames.GlobalSettings);
        await File.WriteAllTextAsync(
            globalSettingsPath,
            "{\"theme\":\"Mocha\"}",
            TestContext.Current.CancellationToken);

        using var service = new SettingsService(_tempPath);
        var settings = await service.LoadAsync();

        Assert.Equal("Mocha", settings.Theme, ignoreCase: true);
        Assert.Equal(ThemePreferenceMode.Manual, settings.ThemeMode);
    }

    [Fact]
    public async Task SaveAndLoadAsync_ThemeModeRoundTrips()
    {
        using var service = new SettingsService(_tempPath);
        _ = await service.LoadAsync();
        service.Current.Theme = "Latte";
        service.Current.ThemeMode = ThemePreferenceMode.Manual;
        await service.SaveAsync();

        using var reloaded = new SettingsService(_tempPath);
        var settings = await reloaded.LoadAsync();

        Assert.Equal("Latte", settings.Theme);
        Assert.Equal(ThemePreferenceMode.Manual, settings.ThemeMode);
    }
    [Fact]
    public async Task SaveAndLoadAsync_PreservesLanguage()
    {
        using var service = new SettingsService(_tempPath);
        _ = await service.LoadAsync();
        service.Current.Language = "tr";
        await service.SaveAsync();

        using var reloaded = new SettingsService(_tempPath);
        var settings = await reloaded.LoadAsync();

        Assert.Equal("tr", settings.Language);
    }

    [Fact]
    public async Task LoadAsync_WhenSettingsValuesRequireNormalization_ReturnsNormalizedValues()
    {
        string rawJson = "{\n  \"playbackSpeed\": 999.0,\n  \"loopDelayMs\": -15,\n  \"loopDelayMinMs\": 120,\n  \"loopDelayMaxMs\": 10\n}" + '\n';
        _ = Directory.CreateDirectory(Path.GetDirectoryName(DefaultPersistedProfileSettingsPath)!);
        await File.WriteAllTextAsync(DefaultPersistedProfileSettingsPath, rawJson, TestContext.Current.CancellationToken);
        using var service = new SettingsService(_tempPath);

        var result = await service.LoadAsync();

        _ = result.PlaybackSpeed.Should().Be(10.0);
        _ = result.LoopDelayMs.Should().Be(0);
        _ = result.LoopDelayMinMs.Should().Be(120);
        _ = result.LoopDelayMaxMs.Should().Be(120);
    }

    [Fact]
    public async Task LoadAsync_WhenFilesMissing_UsesDefaultsWithoutWritingFiles()
    {
        using var service = new SettingsService(_tempPath);

        var result = await service.LoadAsync();

        _ = result.PlaybackSpeed.Should().Be(1.0);
        _ = Directory.EnumerateFiles(_tempPath, "*", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAndLoadAsync_RoundTrip_PreservesSettings()
    {
        using var service = new SettingsService(_tempPath);
        _ = await service.LoadAsync();

        service.Current.EnableTextExpansion = true;
        service.Current.CountdownSeconds = 3;
        service.Current.ForceRelativeCoordinates = true;
        service.Current.UseLogicalRelativeCoordinates = true;
        service.Current.PlaybackSpeed = 3.0;
        service.Current.IsLooping = true;
        service.Current.LoopCount = 5;
        service.Current.LoopDelayMs = 150;
        service.Current.UseRandomLoopDelay = true;
        service.Current.LoopDelayMinMs = 100;
        service.Current.LoopDelayMaxMs = 250;
        service.Current.StartMinimized = true;
        service.Current.SuppressFastLoopWarning = true;
        service.Current.MacOSScreenRecordingOnboardingCompleted = true;

        await service.SaveAsync();

        using var newService = new SettingsService(_tempPath);
        var loaded = await newService.LoadAsync();

        _ = loaded.EnableTextExpansion.Should().BeTrue();
        _ = loaded.CountdownSeconds.Should().Be(3);
        _ = loaded.ForceRelativeCoordinates.Should().BeTrue();
        _ = loaded.UseLogicalRelativeCoordinates.Should().BeTrue();
        _ = loaded.PlaybackSpeed.Should().Be(3.0);
        _ = loaded.IsLooping.Should().BeTrue();
        _ = loaded.LoopCount.Should().Be(5);
        _ = loaded.LoopDelayMs.Should().Be(150);
        _ = loaded.UseRandomLoopDelay.Should().BeTrue();
        _ = loaded.LoopDelayMinMs.Should().Be(100);
        _ = loaded.LoopDelayMaxMs.Should().Be(250);
        _ = loaded.StartMinimized.Should().BeTrue();
        _ = loaded.SuppressFastLoopWarning.Should().BeTrue();
        _ = loaded.MacOSScreenRecordingOnboardingCompleted.Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoadAsync_WhenFileCorrupted_ReturnsDefaultsAndKeepsCorruptFileAfterSave(bool globalFile)
    {
        var corruptPath = globalFile ? Path.Combine(_tempPath, ConfigFileNames.GlobalSettings) : DefaultPersistedProfileSettingsPath;
        _ = Directory.CreateDirectory(Path.GetDirectoryName(corruptPath)!);
        const string Garbage = "{ not json: the user's hand edited file";
        await File.WriteAllTextAsync(corruptPath, Garbage, TestContext.Current.CancellationToken);
        using var service = new SettingsService(_tempPath);

        var result = await service.LoadAsync();
        var loadedSpeed = result.PlaybackSpeed;
        service.Current.PlaybackSpeed = 4.0;
        await service.SaveAsync();

        Assert.Equal(1.0, loadedSpeed);
        var preserved = Directory.EnumerateFiles(Path.GetDirectoryName(corruptPath)!, Path.GetFileName(corruptPath) + ".*.corrupt").Single();
        Assert.Equal(Garbage, await File.ReadAllTextAsync(preserved, TestContext.Current.CancellationToken));
        using var reloaded = new SettingsService(_tempPath);
        Assert.Equal(4.0, (await reloaded.LoadAsync()).PlaybackSpeed);
    }

    [Fact]
    public async Task SaveAsync_WhenSettingsFileCouldNotBeRead_DoesNotOverwriteIt()
    {
        _ = Directory.CreateDirectory(Path.GetDirectoryName(DefaultPersistedProfileSettingsPath)!);
        var original = JsonSerializer.Serialize(
            new PersistedProfileSettings { PlaybackSpeed = 6 },
            CrossMacroJsonContext.Default.PersistedProfileSettings);
        await File.WriteAllTextAsync(DefaultPersistedProfileSettingsPath, original, TestContext.Current.CancellationToken);
        using var service = new SettingsService(_tempPath);

        double loadedSpeed;
        using (new FileStream(DefaultPersistedProfileSettingsPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var loaded = await service.LoadAsync();
            loadedSpeed = loaded.PlaybackSpeed;
            service.Current.PlaybackSpeed = 4.0;
            await service.SaveAsync();
        }

        Assert.Equal(1.0, loadedSpeed);
        Assert.Equal(original, await File.ReadAllTextAsync(DefaultPersistedProfileSettingsPath, TestContext.Current.CancellationToken));
        Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(DefaultPersistedProfileSettingsPath)!, "*.corrupt"));
    }

    [Fact]
    public async Task SaveAsync_WhenWriteFails_Throws()
    {
        var blockingPath = Path.Combine(_tempPath, "not-a-directory");
        await File.WriteAllTextAsync(blockingPath, "blocking file", TestContext.Current.CancellationToken);
        using var service = new SettingsService(blockingPath);

        var act = async () => await service.SaveAsync();

        _ = await act.Should().ThrowAsync<IOException>();
    }

    [Fact]
    public async Task ReloadAsync_PendingDebouncedSnapshot_IsFlushedBeforeProfilePublication()
    {
        await AssertProfileSnapshotRemainsBoundToItsCapturedScopeAsync(debounce: true);
    }

    [Fact]
    public async Task Save_ProfileChangesDuringSnapshotCapture_DoesNotWriteNewProfileIntoOldPath()
    {
        await AssertProfileSnapshotRemainsBoundToItsCapturedScopeAsync(debounce: false);
    }

    private async Task AssertProfileSnapshotRemainsBoundToItsCapturedScopeAsync(bool debounce)
    {
        var profileDirectory = Path.Combine(_tempPath, ConfigFileNames.ProfilesDirectory, "other");
        _ = Directory.CreateDirectory(profileDirectory);
        var newProfilePath = Path.Combine(profileDirectory, ConfigFileNames.Settings);
        await File.WriteAllTextAsync(newProfilePath, JsonSerializer.Serialize(
            new PersistedProfileSettings { PlaybackSpeed = 7 }, CrossMacroJsonContext.Default.PersistedProfileSettings), TestContext.Current.CancellationToken);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(DefaultPersistedProfileSettingsPath)!);
        var originalProfile = JsonSerializer.Serialize(
            new PersistedProfileSettings { PlaybackSpeed = 3 }, CrossMacroJsonContext.Default.PersistedProfileSettings);
        await File.WriteAllTextAsync(DefaultPersistedProfileSettingsPath, originalProfile, TestContext.Current.CancellationToken);
        var captureStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var resumeCapture = new ManualResetEventSlim();
        using var service = new SettingsService(_tempPath, settings =>
        {
            _ = captureStarted.TrySetResult();
            if (!resumeCapture.Wait(TimeSpan.FromSeconds(5))) { throw new TimeoutException("Snapshot capture was not released."); }
            return CrossMacro.Infrastructure.Persistence.Settings.SettingsPersistenceMapper.ToProfile(settings);
        });
        _ = await service.LoadAsync();
        service.Current.PlaybackSpeed = 2;
        service.Current.EnableTrayIcon = true;

        // Save reaches snapshot capture before it owns the I/O gate. The profile reload must
        // be able to finish here, and the resumed save must reject its now-obsolete scope.
        var save = Task.Run(async () =>
        {
            if (debounce)
            {
                var pending = service.SaveAfterIdleAsync();
                await service.FlushPendingSaveAsync(TestContext.Current.CancellationToken);
                await pending;
            }
            else { await service.SaveAsync(); }
        }, TestContext.Current.CancellationToken);
        Task? reload = null;
        try
        {
            await captureStarted.Task.WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System, TestContext.Current.CancellationToken);
            reload = service.ReloadAsync(profileDirectory);
            if (debounce)
            {
                Assert.False(reload.IsCompleted);
                Assert.Equal(2, service.Current.PlaybackSpeed);
            }
            else
            {
                await reload.WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System, TestContext.Current.CancellationToken);
                Assert.Equal(7, service.Current.PlaybackSpeed);
            }
        }
        finally
        {
            resumeCapture.Set();
            await save.WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System, TestContext.Current.CancellationToken);
            if (reload is not null) { await reload.WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System, TestContext.Current.CancellationToken); }
        }

        var oldProfileJson = await File.ReadAllTextAsync(DefaultPersistedProfileSettingsPath, TestContext.Current.CancellationToken);
        if (debounce)
        {
            Assert.Equal(2, JsonSerializer.Deserialize(oldProfileJson, CrossMacroJsonContext.Default.PersistedProfileSettings)?.PlaybackSpeed);
        }
        else { Assert.Equal(originalProfile, oldProfileJson); }
        var persistedNewProfile = JsonSerializer.Deserialize(await File.ReadAllTextAsync(newProfilePath, TestContext.Current.CancellationToken), CrossMacroJsonContext.Default.PersistedProfileSettings);
        Assert.NotNull(persistedNewProfile);
        Assert.Equal(7, persistedNewProfile.PlaybackSpeed);
        using var reloaded = new SettingsService(_tempPath);
        Assert.True((await reloaded.LoadAsync()).EnableTrayIcon);
    }

    [Fact]
    public async Task SaveAsync_QueuedSnapshotsAreWrittenInCallOrder()
    {
        using var service = new SettingsService(_tempPath);
        _ = await service.LoadAsync();

        service.Current.PlaybackSpeed = 2.0;
        var firstSave = service.SaveAsync();
        service.Current.PlaybackSpeed = 3.0;
        var secondSave = service.SaveAsync();

        await Task.WhenAll(firstSave, secondSave);

        using var reloaded = new SettingsService(_tempPath);
        var loaded = await reloaded.LoadAsync();
        _ = loaded.PlaybackSpeed.Should().Be(3.0);
    }
}
