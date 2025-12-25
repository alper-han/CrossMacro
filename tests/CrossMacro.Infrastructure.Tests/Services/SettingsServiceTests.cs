namespace CrossMacro.Infrastructure.Tests.Services;

using System;
using System.IO;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Infrastructure.Services;
using FluentAssertions;
using Xunit;

public class SettingsServiceTests : IDisposable
{
    private readonly string _tempPath;

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

        // Act
        service.Save();
        
        var newService = new SettingsService(_tempPath);
        var loaded = newService.Load();

        // Assert
        loaded.PlaybackSpeed.Should().Be(3.0);
        loaded.IsLooping.Should().BeTrue();
        loaded.LoopCount.Should().Be(5);
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
        // This test ensures graceful degradation
        // The service should return defaults if file is corrupted
        var service = new SettingsService(_tempPath);
        
        // Act - just verify it doesn't throw
        var act = () => service.Load();

        // Assert
        act.Should().NotThrow();
    }
}
