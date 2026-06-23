namespace CrossMacro.Infrastructure.Tests.Services;

using System;
using System.IO;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Infrastructure;
using CrossMacro.Infrastructure.Services;
using FluentAssertions;
using Xunit;

public class SettingsServiceTests : IDisposable
{
    private readonly string _tempPath;

    private string DefaultProfileSettingsPath => Path.Combine(
        _tempPath,
        ConfigFileNames.ProfilesDirectory,
        "default",
        ConfigFileNames.Settings);

    public SettingsServiceTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), "CrossMacroSettingsTests_" + Guid.NewGuid());
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
    public void Current_Initially_ReturnsDefaultSettings()
    {
        // Arrange
        var service = new SettingsService(_tempPath);

        // Assert
        service.Current.Should().NotBeNull();
    }

    [Fact]
    public void Load_ReturnsSettings()
    {
        // Arrange
        var service = new SettingsService(_tempPath);

        // Act
        var result = service.Load();

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task LoadAsync_ReturnsSettings()
    {
        // Arrange
        var service = new SettingsService(_tempPath);

        // Act
        var result = await service.LoadAsync();

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public void Save_DoesNotThrow()
    {
        // Arrange
        var service = new SettingsService(_tempPath);
        service.Load();
        service.Current.PlaybackSpeed = 2.0;

        // Act
        var act = () => service.Save();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task SaveAsync_DoesNotThrow()
    {
        // Arrange
        var service = new SettingsService(_tempPath);
        await service.LoadAsync();
        service.Current.PlaybackSpeed = 1.5;

        // Act
        var act = async () => await service.SaveAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void SaveAndLoad_RoundTrip_PreservesSettings()
    {
        // Arrange
        var service = new SettingsService(_tempPath);
        service.Load();
        
        service.Current.PlaybackSpeed = 3.0;
        service.Current.IsLooping = true;
        service.Current.LoopCount = 5;
        service.Current.LoopDelayMs = 150;
        service.Current.UseRandomLoopDelay = true;
        service.Current.LoopDelayMinMs = 100;
        service.Current.LoopDelayMaxMs = 250;
        service.Current.StartMinimized = true;
        service.Current.PortalScreenCastRestoreToken = "portal-token-1";

        // Act
        service.Save();
        
        var newService = new SettingsService(_tempPath);
        var loaded = newService.Load();

        // Assert
        loaded.PlaybackSpeed.Should().Be(3.0);
        loaded.IsLooping.Should().BeTrue();
        loaded.LoopCount.Should().Be(5);
        loaded.LoopDelayMs.Should().Be(150);
        loaded.UseRandomLoopDelay.Should().BeTrue();
        loaded.LoopDelayMinMs.Should().Be(100);
        loaded.LoopDelayMaxMs.Should().Be(250);
        loaded.StartMinimized.Should().BeTrue();
        loaded.PortalScreenCastRestoreToken.Should().Be("portal-token-1");
    }

    [Fact]
    public void Load_WhenSettingsValuesRequireNormalization_ReturnsNormalizedValues()
    {
        // Arrange
        var service = new SettingsService(_tempPath);
        var rawJson = """
            {
              "playbackSpeed": 999.0,
              "loopDelayMs": -15,
              "loopDelayMinMs": 120,
              "loopDelayMaxMs": 10
            }
            """;
        Directory.CreateDirectory(Path.GetDirectoryName(DefaultProfileSettingsPath)!);
        File.WriteAllText(DefaultProfileSettingsPath, rawJson);

        // Act
        var result = service.Load();

        // Assert
        result.PlaybackSpeed.Should().Be(10.0);
        result.LoopDelayMs.Should().Be(0);
        result.LoopDelayMinMs.Should().Be(120);
        result.LoopDelayMaxMs.Should().Be(120);
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
        result.Should().NotBeNull();
        File.Exists(globalSettingsPath).Should().BeTrue();
        File.Exists(DefaultProfileSettingsPath).Should().BeTrue();
    }

    [Fact]
    public async Task SaveAndLoadAsync_RoundTrip_PreservesSettings()
    {
        // Arrange
        var service = new SettingsService(_tempPath);
        await service.LoadAsync();
        
        service.Current.EnableTextExpansion = true;
        service.Current.CountdownSeconds = 3;

        // Act
        await service.SaveAsync();
        
        var newService = new SettingsService(_tempPath);
        var loaded = await newService.LoadAsync();

        // Assert
        loaded.EnableTextExpansion.Should().BeTrue();
        loaded.CountdownSeconds.Should().Be(3);
    }

    [Fact]
    public void Load_WhenFileCorrupted_ReturnsDefaults()
    {
        // Arrange
        var service = new SettingsService(_tempPath);
        // Ensure file exists but with garbage content
        Directory.CreateDirectory(_tempPath);
        Directory.CreateDirectory(Path.GetDirectoryName(DefaultProfileSettingsPath)!);
        File.WriteAllText(DefaultProfileSettingsPath, "{ invalid_json }");

        // Act
        var result = service.Load();

        // Assert
        result.Should().NotBeNull();
        // Defaults check (assuming defaults are specific values, e.g. PlaybackSpeed = 1.0)
        result.PlaybackSpeed.Should().Be(1.0); 
    }

    [Fact]
    public async Task LoadAsync_WhenFileCorrupted_ReturnsDefaults()
    {
        // Arrange
        var service = new SettingsService(_tempPath);
        Directory.CreateDirectory(_tempPath);
        Directory.CreateDirectory(Path.GetDirectoryName(DefaultProfileSettingsPath)!);
        await File.WriteAllTextAsync(DefaultProfileSettingsPath, "NOT JSON AT ALL");

        // Act
        var result = await service.LoadAsync();

        // Assert
        result.Should().NotBeNull();
        result.PlaybackSpeed.Should().Be(1.0);
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
        act.Should().Throw<IOException>();
    }
}
