namespace CrossMacro.Infrastructure.Tests.Services;


public sealed class HotkeyConfigurationServiceTests : IDisposable
{
    private readonly string _tempPath;

    public HotkeyConfigurationServiceTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), "CrossMacroTests_" + Guid.NewGuid());
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
    public void Load_ReturnsValidSettings()
    {
        // Arrange
        var service = new HotkeyConfigurationService(_tempPath);

        // Act
        var result = service.Load();

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.RecordingHotkey.Should().NotBeNullOrEmpty();
        _ = result.PlaybackHotkey.Should().NotBeNullOrEmpty();
        _ = result.PauseHotkey.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task LoadAsync_ReturnsValidSettings()
    {
        // Arrange
        var service = new HotkeyConfigurationService(_tempPath);

        // Act
        var result = await service.LoadAsync();

        // Assert
        _ = result.Should().NotBeNull();
        _ = result.RecordingHotkey.Should().NotBeNullOrEmpty();
        _ = result.PlaybackHotkey.Should().NotBeNullOrEmpty();
        _ = result.PauseHotkey.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Save_ValidSettings_DoesNotThrow()
    {
        // Arrange
        var service = new HotkeyConfigurationService(_tempPath);
        var settings = new HotkeySettings
        {
            RecordingHotkey = "Ctrl+R",
            PlaybackHotkey = "Ctrl+P",
            PauseHotkey = "Ctrl+Space",
        };

        // Act
        var act = () => service.Save(settings);

        // Assert
        _ = act.Should().NotThrow();
    }

    [Fact]
    public void SaveAndLoad_RoundTrip_PreservesCustomHotkeys()
    {
        // Arrange
        var service = new HotkeyConfigurationService(_tempPath);
        var customSettings = new HotkeySettings
        {
            RecordingHotkey = "F1",
            PlaybackHotkey = "F2",
            PauseHotkey = "F3",
        };

        // Act
        service.Save(customSettings);
        var loaded = service.Load();

        // Assert
        _ = loaded.RecordingHotkey.Should().Be("F1");
        _ = loaded.PlaybackHotkey.Should().Be("F2");
        _ = loaded.PauseHotkey.Should().Be("F3");
    }

    [Fact]
    public async Task CapturedSaveRequest_RetainsOriginalPathAfterProfileReload()
    {
        var firstProfile = Path.Combine(_tempPath, "first");
        var secondProfile = Path.Combine(_tempPath, "second");
        _ = Directory.CreateDirectory(firstProfile);
        _ = Directory.CreateDirectory(secondProfile);
        var service = new HotkeyConfigurationService(firstProfile);
        var settings = new HotkeySettings
        {
            RecordingHotkey = "F1",
            PlaybackHotkey = "F2",
            PauseHotkey = "F3",
        };

        var request = service.CaptureSaveRequest(settings);
        await service.ReloadAsync(secondProfile);
        _ = service.TrySave(request).Should().BeTrue();

        _ = File.Exists(Path.Combine(firstProfile, "hotkeys.json")).Should().BeTrue();
        _ = File.Exists(Path.Combine(secondProfile, "hotkeys.json")).Should().BeFalse();
    }

    [Fact]
    public async Task SaveAndLoadAsync_RoundTrip_PreservesCustomHotkeys()
    {
        // Arrange
        var service = new HotkeyConfigurationService(_tempPath);
        var customSettings = new HotkeySettings
        {
            RecordingHotkey = "Super+R",
            PlaybackHotkey = "Super+P",
            PauseHotkey = "Super+S",
        };

        // Act
        service.Save(customSettings);
        var loaded = await service.LoadAsync();

        // Assert
        _ = loaded.RecordingHotkey.Should().Be("Super+R");
        _ = loaded.PlaybackHotkey.Should().Be("Super+P");
        _ = loaded.PauseHotkey.Should().Be("Super+S");
    }

    [Fact]
    public void Load_WhenFileCorrupted_ReturnsDefaults()
    {
        // Arrange
        var service = new HotkeyConfigurationService(_tempPath);
        File.WriteAllText(Path.Combine(_tempPath, "hotkeys.json"), "{ invalid json }");

        // Act
        var loaded = service.Load();

        // Assert
        _ = loaded.RecordingHotkey.Should().Be("F8");
        _ = loaded.PlaybackHotkey.Should().Be("F9");
        _ = loaded.PauseHotkey.Should().Be("F10");
    }

    [Fact]
    public void Save_WhenWriteFails_SwallowsException()
    {
        // Arrange
        var serviceRoot = Path.Combine(_tempPath, "config-root");
        _ = Directory.CreateDirectory(serviceRoot);
        var service = new HotkeyConfigurationService(serviceRoot);
        Directory.Delete(serviceRoot);
        File.WriteAllText(serviceRoot, "blocking file");

        var settings = new HotkeySettings
        {
            RecordingHotkey = "Ctrl+R",
            PlaybackHotkey = "Ctrl+P",
            PauseHotkey = "Ctrl+Space",
        };

        // Act
        var act = () => service.Save(settings);

        // Assert
        _ = act.Should().NotThrow();
    }
}
