using System;
using System.Threading.Tasks;
using CrossMacro.Core;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CrossMacro.Infrastructure.Tests.Services;

public class MacroScheduledTaskExecutorTests
{
    private readonly IMacroFileManager _fileManager;
    private readonly IMacroPlayer _player;
    private readonly ITimeProvider _timeProvider;
    private readonly MacroScheduledTaskExecutor _executor;

    public MacroScheduledTaskExecutorTests()
    {
        _fileManager = Substitute.For<IMacroFileManager>();
        _player = Substitute.For<IMacroPlayer>();
        _timeProvider = Substitute.For<ITimeProvider>();
        
        // Mock time
        _timeProvider.UtcNow.Returns(new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc));

        _executor = new MacroScheduledTaskExecutor(
            _fileManager,
            () => _player,
            _timeProvider);
    }

    [Fact]
    public async Task ExecuteAsync_WhenFileDoesNotExist_UpdatesStatusAndFails()
    {
        // Arrange
        var task = new ScheduledTask { MacroFilePath = "nonexistent.macro" };
        // We can't easily mock File.Exists since it's static, but the executor checks string.IsNullOrEmpty first.
        // Wait, the executor calls File.Exists directly. This makes it hard to unit test without IFileSystem.
        // However, we can test the behavior when the file is missing by providing a path that definitely doesn't exist 
        // OR by relying on the fact that we are in a unit test environment where that file likely doesn't exist.
        // A better approach for the future would be IFileSystem, but for now we assume it doesn't exist.
        
        // Act
        await _executor.ExecuteAsync(task);

        // Assert
        task.LastStatus.Should().Be("Macro file not found");
        task.LastRunTime.Should().Be(_timeProvider.UtcNow);
    }

    [Fact]
    public async Task ExecuteAsync_WhenLoadFails_UpdatesStatusAndFails()
    {
        // Arrange
        // We need to bake a real file or use a path that exists? 
        // The current implementation of MacroScheduledTaskExecutor checks File.Exists(task.MacroFilePath).
        // If we can't mock File.Exists, we can't fully test the success path or load failure path *unless* we create a temp file.
        
        var tempFile = Path.GetTempFileName();
        try
        {
            var task = new ScheduledTask { MacroFilePath = tempFile };
            _fileManager.LoadAsync(tempFile).Returns((MacroSequence)null!);

            // Act
            await _executor.ExecuteAsync(task);

            // Assert
            task.LastStatus.Should().Be("Failed to load macro");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenSuccess_UpdatesStatusAndNextRunTime()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            var task = new ScheduledTask 
            { 
                MacroFilePath = tempFile,
                Type = ScheduleType.Interval,
                IntervalValue = 10,
                IntervalUnit = IntervalUnit.Seconds,
                IsEnabled = true
            };
            
            var macro = new MacroSequence { Name = "Test MacroSequence" };
            _fileManager.LoadAsync(tempFile).Returns(macro);

            // Act
            await _executor.ExecuteAsync(task);

            // Assert
            await _player.Received(1).PlayAsync(macro, Arg.Any<PlaybackOptions>());
            task.LastStatus.Should().Be("Success");
            task.LastRunTime.Should().Be(_timeProvider.UtcNow);
            
            // Should verify next run time calculation
            // The NextRunTime logic depends on current time + interval. 
            // Since we mocked UtcNow, it should be predictable.
            task.NextRunTime.Should().Be(_timeProvider.UtcNow.AddSeconds(10));
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
    
    [Fact]
    public async Task ExecuteAsync_WhenOneTimeTaskSuccess_DisablesTask()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            var task = new ScheduledTask 
            { 
                MacroFilePath = tempFile,
                Type = ScheduleType.SpecificTime,
                IsEnabled = true
            };
            
            var macro = new MacroSequence();
            _fileManager.LoadAsync(tempFile).Returns(macro);

            // Act
            await _executor.ExecuteAsync(task);

            // Assert
            task.IsEnabled.Should().BeFalse();
            task.NextRunTime.Should().BeNull();
            task.LastStatus.Should().Be("Success");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenPlaybackThrows_UpdatesStatusAndFailsGracefully()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            var task = new ScheduledTask 
            { 
                MacroFilePath = tempFile,
                IsEnabled = true 
            };
            
            var macro = new MacroSequence();
            _fileManager.LoadAsync(tempFile).Returns(macro);
            
            _player.When(p => p.PlayAsync(macro, Arg.Any<PlaybackOptions>()))
                   .Do(x => throw new Exception("Unexpected crash"));

            // Act
            await _executor.ExecuteAsync(task);

            // Assert
            task.LastStatus.Should().Contain("Error");
            task.LastStatus.Should().Contain("Unexpected crash");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenTaskSpeedIsInvalid_UsesNormalizedPlaybackSpeed()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            var task = new ScheduledTask
            {
                MacroFilePath = tempFile,
                PlaybackSpeed = 0.0,
                IsEnabled = true
            };

            var macro = new MacroSequence { Events = { new MacroEvent { Type = EventType.MouseMove, X = 0, Y = 0 } } };
            _fileManager.LoadAsync(tempFile).Returns(macro);

            // Act
            await _executor.ExecuteAsync(task);

            // Assert
            await _player.Received(1).PlayAsync(
                macro,
                Arg.Is<PlaybackOptions>(o => o.SpeedMultiplier == PlaybackOptions.MinSpeedMultiplier));
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
