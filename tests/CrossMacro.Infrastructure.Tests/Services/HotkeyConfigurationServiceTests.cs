namespace CrossMacro.Infrastructure.Tests.Services;

using System;
using System.IO;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Infrastructure.Services;
using FluentAssertions;
using Xunit;

public class HotkeyConfigurationServiceTests : IDisposable
{
    private readonly string _tempPath;

    public HotkeyConfigurationServiceTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), "CrossMacroTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempPath))
        {
            try 
            {
                Directory.Delete(_tempPath, true);
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
        result.Should().NotBeNull();
        result.RecordingHotkey.Should().NotBeNullOrEmpty();
        result.PlaybackHotkey.Should().NotBeNullOrEmpty();
        result.PauseHotkey.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task LoadAsync_ReturnsValidSettings()
    {
        // Arrange
        var service = new HotkeyConfigurationService(_tempPath);

        // Act
        var result = await service.LoadAsync();

        // Assert
        result.Should().NotBeNull();
        result.RecordingHotkey.Should().NotBeNullOrEmpty();
        result.PlaybackHotkey.Should().NotBeNullOrEmpty();
        result.PauseHotkey.Should().NotBeNullOrEmpty();
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
            PauseHotkey = "Ctrl+Space"
        };

        // Act
        var act = () => service.Save(settings);

        // Assert
        act.Should().NotThrow();
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
            PauseHotkey = "F3"
        };

        // Act
        service.Save(customSettings);
        var loaded = service.Load();

        // Assert
        loaded.RecordingHotkey.Should().Be("F1");
        loaded.PlaybackHotkey.Should().Be("F2");
        loaded.PauseHotkey.Should().Be("F3");
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
            PauseHotkey = "Super+S"
        };

        // Act
        service.Save(customSettings);
        var loaded = await service.LoadAsync();

        // Assert
        loaded.RecordingHotkey.Should().Be("Super+R");
        loaded.PlaybackHotkey.Should().Be("Super+P");
        loaded.PauseHotkey.Should().Be("Super+S");
    }
}
