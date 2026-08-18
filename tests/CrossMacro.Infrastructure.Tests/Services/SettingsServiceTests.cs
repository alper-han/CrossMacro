namespace CrossMacro.Infrastructure.Tests.Services;


public sealed class SettingsServiceTests : IDisposable
{
    private static readonly CancellationToken NonCancelableToken = new(canceled: false);
    private readonly string _tempPath;

    private string DefaultProfileSettingsPath => Path.Combine(
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
            try
            {
                Directory.Delete(_tempPath, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public void Current_Initially_ReturnsDefaultSettings()
    {
        // Arrange
        var service = new SettingsService(_tempPath);

        // Assert
        _ = service.Current.Should().NotBeNull();
    }

    [Fact]
    public void Dispose_CanBeCalledRepeatedly()
    {
        var service = new SettingsService(_tempPath);

        var act = () =>
        {
            service.Dispose();
            service.Dispose();
        };

        _ = act.Should().NotThrow();
    }

    [Fact]
    public void Load_ReturnsSettings()
    {
        // Arrange
        var service = new SettingsService(_tempPath);

        // Act
        var result = service.Load();

        // Assert
        _ = result.Should().NotBeNull();
    }

    [Fact]
    public async Task LoadAsync_ReturnsSettings()
    {
        // Arrange
        var service = new SettingsService(_tempPath);

        // Act
        var result = await service.LoadAsync();

        // Assert
        _ = result.Should().NotBeNull();
    }

    [Fact]
    public async Task ReloadAsync_FirstLoadCombinesGlobalAndProfileSettings()
    {
        var profileDirectory = Path.Combine(_tempPath, "profiles", "work");
        _ = Directory.CreateDirectory(profileDirectory);

        await File.WriteAllTextAsync(
            Path.Combine(_tempPath, ConfigFileNames.GlobalSettings),
            JsonSerializer.Serialize(
                new GlobalSettings
                {
                    EnableTrayIcon = true,
                    Language = "tr",
                },
                CrossMacroJsonContext.Default.GlobalSettings),
            NonCancelableToken);
        await File.WriteAllTextAsync(
            Path.Combine(profileDirectory, ConfigFileNames.Settings),
            JsonSerializer.Serialize(
                new ProfileSettings
                {
                    PlaybackSpeed = 2.5,
                    IsLooping = true,
                },
                CrossMacroJsonContext.Default.ProfileSettings),
            NonCancelableToken);

        var service = new SettingsService(_tempPath);

        await service.ReloadAsync(profileDirectory);

        _ = service.Current.EnableTrayIcon.Should().BeTrue();
        _ = service.Current.Language.Should().Be("tr");
        _ = service.Current.PlaybackSpeed.Should().Be(2.5);
        _ = service.Current.IsLooping.Should().BeTrue();
    }

    [Fact]
    public void Save_DoesNotThrow()
    {
        // Arrange
        var service = new SettingsService(_tempPath);
        _ = service.Load();
        service.Current.PlaybackSpeed = 2.0;

        // Act
        var act = () => service.Save();

        // Assert
        _ = act.Should().NotThrow();
    }

    [Fact]
    public async Task SaveAsync_DoesNotThrow()
    {
        // Arrange
        var service = new SettingsService(_tempPath);
        _ = await service.LoadAsync();
        service.Current.PlaybackSpeed = 1.5;

        // Act
        var act = async () => await service.SaveAsync();

        // Assert
        _ = await act.Should().NotThrowAsync();
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

        await service.FlushPendingSaveAsync();
        await firstSave;

        using var reloadedService = new SettingsService(_tempPath);
        var loaded = await reloadedService.LoadAsync();

        _ = loaded.PlaybackSpeed.Should().Be(3.0);
    }

    [Fact]
    public void SaveAndLoad_RoundTrip_PreservesSettings()
    {
        // Arrange
        var service = new SettingsService(_tempPath);
        _ = service.Load();

        service.Current.PlaybackSpeed = 3.0;
        service.Current.IsLooping = true;
        service.Current.LoopCount = 5;
        service.Current.LoopDelayMs = 150;
        service.Current.UseRandomLoopDelay = true;
        service.Current.LoopDelayMinMs = 100;
        service.Current.LoopDelayMaxMs = 250;
        service.Current.StartMinimized = true;
        service.Current.SuppressFastLoopWarning = true;

        // Act
        service.Save();

        var newService = new SettingsService(_tempPath);
        var loaded = newService.Load();

        // Assert
        _ = loaded.PlaybackSpeed.Should().Be(3.0);
        _ = loaded.IsLooping.Should().BeTrue();
        _ = loaded.LoopCount.Should().Be(5);
        _ = loaded.LoopDelayMs.Should().Be(150);
        _ = loaded.UseRandomLoopDelay.Should().BeTrue();
        _ = loaded.LoopDelayMinMs.Should().Be(100);
        _ = loaded.LoopDelayMaxMs.Should().Be(250);
        _ = loaded.StartMinimized.Should().BeTrue();
        _ = loaded.SuppressFastLoopWarning.Should().BeTrue();
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
            NonCancelableToken);

        using var service = new SettingsService(_tempPath);
        _ = await service.LoadAsync();
        await service.SaveAsync();

        var persisted = await File.ReadAllTextAsync(globalSettingsPath, NonCancelableToken);
        Assert.DoesNotContain("portalScreenCastRestore", persisted, StringComparison.Ordinal);
        Assert.Contains("\"theme\": \"Nord\"", persisted, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_WhenSettingsValuesRequireNormalization_ReturnsNormalizedValues()
    {
        // Arrange
        var service = new SettingsService(_tempPath);
        string rawJson = "{\n  \"playbackSpeed\": 999.0,\n  \"loopDelayMs\": -15,\n  \"loopDelayMinMs\": 120,\n  \"loopDelayMaxMs\": 10\n}" + '\n';
        _ = Directory.CreateDirectory(Path.GetDirectoryName(DefaultProfileSettingsPath)!);
        File.WriteAllText(DefaultProfileSettingsPath, rawJson);

        // Act
        var result = service.Load();

        // Assert
        _ = result.PlaybackSpeed.Should().Be(10.0);
        _ = result.LoopDelayMs.Should().Be(0);
        _ = result.LoopDelayMinMs.Should().Be(120);
        _ = result.LoopDelayMaxMs.Should().Be(120);
    }

    [Fact]
    public void Load_WhenFileMissing_PersistsDefaultSettingsFile()
    {
        // Arrange
        var service = new SettingsService(_tempPath);
        var globalSettingsPath = Path.Combine(_tempPath, ConfigFileNames.GlobalSettings);

        // Act
        var result = service.Load();

        // Assert
        _ = result.Should().NotBeNull();
        _ = File.Exists(globalSettingsPath).Should().BeTrue();
        _ = File.Exists(DefaultProfileSettingsPath).Should().BeTrue();
    }

    [Fact]
    public async Task SaveAndLoadAsync_RoundTrip_PreservesSettings()
    {
        // Arrange
        var service = new SettingsService(_tempPath);
        _ = await service.LoadAsync();

        service.Current.EnableTextExpansion = true;
        service.Current.CountdownSeconds = 3;

        // Act
        await service.SaveAsync();

        var newService = new SettingsService(_tempPath);
        var loaded = await newService.LoadAsync();

        // Assert
        _ = loaded.EnableTextExpansion.Should().BeTrue();
        _ = loaded.CountdownSeconds.Should().Be(3);
    }

    [Fact]
    public void Load_WhenFileCorrupted_ReturnsDefaults()
    {
        // Arrange
        var service = new SettingsService(_tempPath);
        // Ensure file exists but with garbage content
        _ = Directory.CreateDirectory(_tempPath);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(DefaultProfileSettingsPath)!);
        File.WriteAllText(DefaultProfileSettingsPath, "{ invalid_json }");

        // Act
        var result = service.Load();

        // Assert
        _ = result.Should().NotBeNull();
        // Defaults check (assuming defaults are specific values, e.g. PlaybackSpeed = 1.0)
        _ = result.PlaybackSpeed.Should().Be(1.0);
    }

    [Fact]
    public async Task LoadAsync_WhenFileCorrupted_ReturnsDefaults()
    {
        // Arrange
        var service = new SettingsService(_tempPath);
        _ = Directory.CreateDirectory(_tempPath);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(DefaultProfileSettingsPath)!);
        await File.WriteAllTextAsync(DefaultProfileSettingsPath, "NOT JSON AT ALL", NonCancelableToken);

        // Act
        var result = await service.LoadAsync();

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.PlaybackSpeed.Should().Be(1.0);
    }

    [Fact]
    public void Save_WhenWriteFails_Throws()
    {
        // Arrange
        var blockingPath = Path.Combine(_tempPath, "not-a-directory");
        File.WriteAllText(blockingPath, "blocking file");
        var service = new SettingsService(blockingPath);

        // Act
        var act = () => service.Save();

        // Assert
        _ = act.Should().Throw<IOException>();
    }
    [Fact]
    public async Task SaveAsync_WhenWriteFails_Throws()
    {
        // Arrange
        var blockingPath = Path.Combine(_tempPath, "not-a-directory");
        File.WriteAllText(blockingPath, "blocking file");
        var service = new SettingsService(blockingPath);

        // Act
        var act = async () => await service.SaveAsync();

        // Assert
        _ = await act.Should().ThrowAsync<IOException>();
    }

    [Fact]
    public async Task SaveAsync_QueuedSnapshotsAreWrittenInCallOrder()
    {
        var service = new SettingsService(_tempPath);
        _ = await service.LoadAsync();

        service.Current.PlaybackSpeed = 2.0;
        var firstSave = service.SaveAsync();
        service.Current.PlaybackSpeed = 3.0;
        var secondSave = service.SaveAsync();

        await Task.WhenAll(firstSave, secondSave);

        var loaded = await new SettingsService(_tempPath).LoadAsync();
        _ = loaded.PlaybackSpeed.Should().Be(3.0);
    }
}
