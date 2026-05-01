using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Serialization;
using CrossMacro.Infrastructure.Services;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CrossMacro.Infrastructure.Tests.Services;

public class ShortcutServiceTests : IDisposable
{
    private readonly IMacroFileManager _fileManager;
    private readonly Func<IMacroPlayer> _playerFactory;
    private readonly IMacroPlayer _player;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly ShortcutService _service;
    private readonly string _testRootDirectory;
    private readonly string _shortcutsFilePath;

    public ShortcutServiceTests()
    {
        _testRootDirectory = Path.Combine(
            Path.GetTempPath(),
            "crossmacro-tests",
            nameof(ShortcutServiceTests),
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRootDirectory);
        _shortcutsFilePath = Path.Combine(_testRootDirectory, "shortcuts.json");

        _fileManager = Substitute.For<IMacroFileManager>();
        _player = Substitute.For<IMacroPlayer>();
        _playerFactory = () => _player;
        _hotkeyService = Substitute.For<IGlobalHotkeyService>();
        
        _service = new ShortcutService(_fileManager, _playerFactory, _hotkeyService, _shortcutsFilePath);
    }

    public void Dispose()
    {
        try
        {
            _service.Dispose();
        }
        catch
        {
        }

        try
        {
            if (Directory.Exists(_testRootDirectory))
            {
                Directory.Delete(_testRootDirectory, recursive: true);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public void Start_SubscribesToHotkeyService()
    {
        _service.Start();
        
        // We can verify this by checking if IsListening is true, 
        // verifying event subscription is hard with NSubstitute unless we inspect calls to add_Event.
        // But implementation sets IsListening.
        _service.IsListening.Should().BeTrue();
    }

    [Fact]
    public void Stop_UnsubscribesAndSetsListeningFalse()
    {
        _service.Start();
        _service.Stop();
        
        _service.IsListening.Should().BeFalse();
    }

    [Fact]
    public void AddTask_AddsToCollection()
    {
        var task = new ShortcutTask();
        _service.AddTask(task);
        _service.Tasks.Should().Contain(task);
    }

    [Fact]
    public void RemoveTask_RemovesFromCollection()
    {
        var task = new ShortcutTask();
        _service.AddTask(task);
        _service.RemoveTask(task.Id);
        _service.Tasks.Should().NotContain(task);
    }

    [Fact]
    public async Task OnRawInputReceived_ExecutesMatchingTask()
    {
        // Arrange
        var task = new ShortcutTask 
        { 
            Name = "Test", 
            MacroFilePath = "test.macro", 
            HotkeyString = "F5",
            PlaybackSpeed = 0.0
        };
        task.IsEnabled = true;
        _service.AddTask(task);

        _fileManager.LoadAsync(Arg.Any<string>())
            .Returns(Task.FromResult<MacroSequence?>(new MacroSequence { Events = { new MacroEvent() } }));
        _player
            .PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>())
            .Returns(Task.CompletedTask);

        var executed = new TaskCompletionSource<ShortcutExecutedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        _service.ShortcutExecuted += (_, e) =>
        {
            if (e.Task.Id == task.Id)
            {
                executed.TrySetResult(e);
            }
        };

        _service.Start();

        var tempFile = Path.GetTempFileName();
        task.MacroFilePath = tempFile;

        try
        {
            // Act
            _hotkeyService.RawInputReceived += Raise.Event<EventHandler<RawHotkeyInputEventArgs>>(
                this, 
                new RawHotkeyInputEventArgs(0, new HashSet<int>(), "F5"));
            var result = await executed.Task.WaitAsync(TimeSpan.FromSeconds(2));

            // Assert
            result.Success.Should().BeTrue();
            await _player.Received(1).PlayAsync(
                Arg.Any<MacroSequence>(),
                Arg.Is<PlaybackOptions>(o => o.SpeedMultiplier == PlaybackOptions.MinSpeedMultiplier));
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task OnRawInputReceived_DoesNotExecute_IfDisabled()
    {
        // Arrange
        var task = new ShortcutTask 
        { 
            HotkeyString = "F5", 
            MacroFilePath = "test.macro" 
        };
        task.IsEnabled = false;
        _service.AddTask(task);
        
        _service.Start();

        // Act
        _hotkeyService.RawInputReceived += Raise.Event<EventHandler<RawHotkeyInputEventArgs>>(
            this, 
            new RawHotkeyInputEventArgs(0, new HashSet<int>(), "F5"));

        // Assert
        await _player.DidNotReceive().PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>());
    }

    [Fact]
    public async Task OnRawKeyReleased_RunWhileHeldHotkey_DoesNotThrowAndStopsPlayer()
    {
        // Arrange
        var task = new ShortcutTask
        {
            Name = "Held Macro",
            HotkeyString = "Ctrl+F5",
            RunWhileHeld = true
        };

        var tempFile = Path.GetTempFileName();
        task.MacroFilePath = tempFile;
        task.IsEnabled = true;
        _service.AddTask(task);

        var startedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releasePlaybackTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        _fileManager.LoadAsync(tempFile).Returns(Task.FromResult<MacroSequence?>(new MacroSequence
        {
            Events = { new MacroEvent { Type = EventType.KeyPress, KeyCode = 30, Timestamp = 0 } }
        }));

        _player
            .PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                startedTcs.TrySetResult(true);
                return releasePlaybackTcs.Task;
            });

        _service.Start();

        try
        {
            _hotkeyService.RawInputReceived += Raise.Event<EventHandler<RawHotkeyInputEventArgs>>(
                this,
                new RawHotkeyInputEventArgs(63, new HashSet<int> { 29 }, "Ctrl+F5"));

            await startedTcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

            var releaseException = Record.Exception(() =>
                _hotkeyService.RawKeyReleased += Raise.Event<EventHandler<RawHotkeyInputEventArgs>>(
                    this,
                    new RawHotkeyInputEventArgs(63, new HashSet<int> { 29 }, string.Empty)));

            releaseException.Should().BeNull();

            _player.Received(1).Stop();
        }
        finally
        {
            releasePlaybackTcs.TrySetResult(true);
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task RunTaskAsync_WhenCancelledDuringPlayback_UpdatesTaskAsStopped()
    {
        var tempFile = Path.GetTempFileName();
        using var cts = new CancellationTokenSource();

        try
        {
            var task = new ShortcutTask
            {
                Name = "Manual Run",
                HotkeyString = "F5",
                MacroFilePath = tempFile,
                IsEnabled = true
            };

            _service.AddTask(task);
            _fileManager.LoadAsync(tempFile).Returns(Task.FromResult<MacroSequence?>(new MacroSequence
            {
                Events = { new MacroEvent { Type = EventType.KeyPress, KeyCode = 30 } }
            }));

            var playbackStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _player.PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                {
                    playbackStarted.TrySetResult(true);
                    return Task.Delay(Timeout.Infinite, ci.ArgAt<CancellationToken>(2));
                });

            var runTask = _service.RunTaskAsync(task.Id, cts.Token);
            await playbackStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            cts.Cancel();
            await runTask;

            task.LastStatus.Should().Be("Stopped");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task SaveAsync_AndLoadAsync_RoundTripPreservesPersistedContractAndComputedState()
    {
        var persistedTask = new ShortcutTask
        {
            Name = "Launch Macro",
            MacroFilePath = "/tmp/sample.macro",
            HotkeyString = "Ctrl+Shift+M",
            PlaybackSpeed = 2.5,
            IsEnabled = true,
            LoopEnabled = true,
            RepeatCount = 4,
            RepeatDelayMs = 125,
            LastStatus = "Success",
            LastTriggeredTime = new DateTime(2026, 4, 27, 10, 30, 0, DateTimeKind.Utc)
        };

        _service.AddTask(persistedTask);

        await _service.SaveAsync();

        var savedJson = await File.ReadAllTextAsync(_shortcutsFilePath);
        savedJson.Should().Contain("\"macroFilePath\"");
        savedJson.Should().Contain("\"hotkeyString\"");
        savedJson.Should().Contain("\"loopEnabled\": true");
        savedJson.Should().NotContain("\"isLoopEnabled\"");

        var reloadedService = new ShortcutService(_fileManager, _playerFactory, _hotkeyService, _shortcutsFilePath);

        try
        {
            await reloadedService.LoadAsync();

            reloadedService.Tasks.Should().ContainSingle();
            var loadedTask = reloadedService.Tasks[0];
            loadedTask.Name.Should().Be("Launch Macro");
            loadedTask.MacroFilePath.Should().Be("/tmp/sample.macro");
            loadedTask.HotkeyString.Should().Be("Ctrl+Shift+M");
            loadedTask.PlaybackSpeed.Should().Be(2.5);
            loadedTask.IsEnabled.Should().BeTrue();
            loadedTask.LoopEnabled.Should().BeTrue();
            loadedTask.RunWhileHeld.Should().BeFalse();
            loadedTask.IsLoopEnabled.Should().BeTrue();
            loadedTask.CanBeEnabled.Should().BeTrue();
            loadedTask.RepeatCount.Should().Be(4);
            loadedTask.RepeatDelayMs.Should().Be(125);
            loadedTask.LastStatus.Should().Be("Success");
            loadedTask.LastTriggeredTime.Should().Be(new DateTime(2026, 4, 27, 10, 30, 0, DateTimeKind.Utc));
        }
        finally
        {
            reloadedService.Dispose();
        }
    }

    [Fact]
    public async Task LoadAsync_UsesExplicitShortcutTaskContextContract()
    {
        var expectedTasks = new List<ShortcutTask>
        {
            new()
            {
                Name = "Typed Context Task",
                MacroFilePath = "typed.macro",
                HotkeyString = "F6",
                RunWhileHeld = true,
                RepeatDelayMs = 42
            }
        };

        await File.WriteAllTextAsync(
            _shortcutsFilePath,
            System.Text.Json.JsonSerializer.Serialize(expectedTasks, CrossMacroJsonContext.Default.ListShortcutTask));

        await _service.LoadAsync();

        _service.Tasks.Should().ContainSingle();
        var loadedTask = _service.Tasks[0];
        loadedTask.Name.Should().Be("Typed Context Task");
        loadedTask.RunWhileHeld.Should().BeTrue();
        loadedTask.LoopEnabled.Should().BeFalse();
        loadedTask.IsLoopEnabled.Should().BeTrue();
        loadedTask.RepeatDelayMs.Should().Be(42);
    }
}
