using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.UI.Models;
using CrossMacro.UI.Services;
using CrossMacro.UI.ViewModels;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CrossMacro.UI.Tests.ViewModels;

public class PlaybackViewModelTests
{
    private readonly IMacroPlayer _player;
    private readonly ISettingsService _settingsService;
    private readonly AppSettings _settings;
    private readonly LoadedMacroSession _loadedMacroSession;
    private readonly ILocalizationService _localizationService;
    private readonly PlaybackViewModel _viewModel;

    public PlaybackViewModelTests()
    {
        _player = Substitute.For<IMacroPlayer>();
        _settingsService = Substitute.For<ISettingsService>();
        _localizationService = Substitute.For<ILocalizationService>();
        _localizationService.CurrentCulture.Returns(System.Globalization.CultureInfo.GetCultureInfo("en"));
        _localizationService[Arg.Any<string>()].Returns(call => call.Arg<string>() switch
        {
            "Playback_StatusReady" => "[Playback_StatusReady]",
            "Playback_StatusPlaying" => "[Playback_StatusPlaying]",
            "Playback_StatusComplete" => "[Playback_StatusComplete]",
            "Playback_StatusStopped" => "[Playback_StatusStopped]",
            "Playback_StatusPaused" => "[Playback_StatusPaused]",
            "Playback_StatusError" => "[Playback_StatusError] {0}",
            "Playback_StatusWaitingNextSequence" => "[Playback_StatusWaitingNextSequence] {0}",
            "Playback_StatusSequencePlaying" => "[Playback_StatusSequencePlaying] {0} | {1} | {2} | {3} | {4}",
            "Playback_StatusWaitingNextLoop" => "[Playback_StatusWaitingNextLoop] {0}",
            "Playback_StatusLoopInfinite" => "[Playback_StatusLoopInfinite] {0}",
            "Playback_StatusLoopProgress" => "[Playback_StatusLoopProgress] {0} | {1}",
            "Playback_StatusStartingIn" => "[Playback_StatusStartingIn] {0}",
            _ => call.Arg<string>()
        });
        _settings = new AppSettings
        {
            PlaybackSpeed = 1.0,
            IsLooping = false,
            LoopCount = 1,
            LoopDelayMs = 0,
            UseRandomLoopDelay = false,
            LoopDelayMinMs = 0,
            LoopDelayMaxMs = 0,
            CountdownSeconds = 0
        };
        _loadedMacroSession = new LoadedMacroSession(_localizationService);

        _settingsService.Current.Returns(_settings);
        _player.CurrentLoop.Returns(1);
        _player.TotalLoops.Returns(1);
        _player.IsWaitingBetweenLoops.Returns(false);
        _player.PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _viewModel = new PlaybackViewModel(_player, _settingsService, _loadedMacroSession, _localizationService);
    }

    [Fact]
    public void Constructor_InitializesPropertiesFromSettings()
    {
        _viewModel.PlaybackSpeed.Should().Be(1.0);
        _viewModel.IsLooping.Should().BeFalse();
        _viewModel.LoopCount.Should().Be(1);
        _viewModel.LoopDelayMs.Should().Be(0);
        _viewModel.UseRandomLoopDelay.Should().BeFalse();
        _viewModel.LoopDelayMinMs.Should().Be(0);
        _viewModel.LoopDelayMaxMs.Should().Be(0);
    }

    [Fact]
    public void RandomLoopDelay_TogglesVisibleInputs()
    {
        _viewModel.IsLooping = true;

        _viewModel.ShowFixedLoopDelayInput.Should().BeTrue();
        _viewModel.ShowRandomLoopDelayInputs.Should().BeFalse();

        _viewModel.UseRandomLoopDelay = true;

        _viewModel.ShowFixedLoopDelayInput.Should().BeFalse();
        _viewModel.ShowRandomLoopDelayInputs.Should().BeTrue();
    }

    [Fact]
    public void RandomLoopDelay_MaxClampsToMin()
    {
        _viewModel.UseRandomLoopDelay = true;
        _viewModel.LoopDelayMinMs = 300;
        _viewModel.LoopDelayMaxMs = 100;

        _viewModel.LoopDelayMinMs.Should().Be(300);
        _viewModel.LoopDelayMaxMs.Should().Be(300);
    }

    [Fact]
    public async Task PlayMacroAsync_WhenCanPlay_StartsPlayback()
    {
        var macro = CreateMacro();
        _viewModel.SetMacro(macro);
        _viewModel.CanPlayMacroExternal = true;

        await _viewModel.PlayMacroAsync();

        await _player.Received(1).PlayAsync(macro, Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PlayMacroAsync_WhenRandomLoopDelayEnabled_ForwardsRandomDelayOptions()
    {
        var macro = CreateMacro();
        PlaybackOptions? capturedOptions = null;
        _player.PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedOptions = callInfo.ArgAt<PlaybackOptions>(1);
                return Task.CompletedTask;
            });

        _viewModel.SetMacro(macro);
        _viewModel.IsLooping = true;
        _viewModel.LoopCount = 3;
        _viewModel.LoopDelayMs = 90;
        _viewModel.UseRandomLoopDelay = true;
        _viewModel.LoopDelayMinMs = 120;
        _viewModel.LoopDelayMaxMs = 240;

        await _viewModel.PlayMacroAsync();

        capturedOptions.Should().NotBeNull();
        capturedOptions!.Loop.Should().BeTrue();
        capturedOptions.RepeatCount.Should().Be(3);
        capturedOptions.RepeatDelayMs.Should().Be(90);
        capturedOptions.UseRandomRepeatDelay.Should().BeTrue();
        capturedOptions.RepeatDelayMinMs.Should().Be(120);
        capturedOptions.RepeatDelayMaxMs.Should().Be(240);
    }

    [Fact]
    public async Task PlayMacroAsync_WhenCannotPlayExternal_DoesNotStart()
    {
        var macro = CreateMacro();
        _viewModel.SetMacro(macro);
        _viewModel.CanPlayMacroExternal = false;

        await _viewModel.PlayMacroAsync();

        await _player.DidNotReceive().PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PlayMacroAsync_WhenAdvanceSelectionMode_AdvancesToNextLoadedMacro()
    {
        var first = _loadedMacroSession.AddMacro(CreateMacro("First"));
        var second = _loadedMacroSession.AddMacro(CreateMacro("Second"));
        _loadedMacroSession.SelectedMacroItem = first;
        _loadedMacroSession.PlaybackMode = LoadedMacroPlaybackMode.AdvanceSelection;

        await _viewModel.PlayMacroAsync();

        await _player.Received(1).PlayAsync(first.Macro, Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>());
        _loadedMacroSession.SelectedMacroItem.Should().BeSameAs(second);
    }

    [Fact]
    public async Task PlayMacroAsync_WhenSequentialCycleModeAndSelectionIsNull_StartsFromFirstLoadedMacro()
    {
        var first = _loadedMacroSession.AddMacro(CreateMacro("First"));
        _loadedMacroSession.AddMacro(CreateMacro("Second"));
        _loadedMacroSession.SelectedMacroItem = null;
        _loadedMacroSession.PlaybackMode = LoadedMacroPlaybackMode.SequentialCycle;

        var playedMacros = new List<MacroSequence>();
        _player.PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                playedMacros.Add(callInfo.ArgAt<MacroSequence>(0));
                return Task.CompletedTask;
            });

        _viewModel.HasMacro.Should().BeTrue();
        _viewModel.CanPlayMacro.Should().BeTrue();

        await _viewModel.PlayMacroAsync();

        playedMacros.Select(macro => macro.Name).Should().ContainInOrder("First", "Second");
        _loadedMacroSession.SelectedMacroItem.Should().BeSameAs(first);
    }

    [Fact]
    public async Task PlayMacroAsync_WhenSequentialCycleMode_PlaysFromSelectedItemAndWrapsToStart()
    {
        var first = _loadedMacroSession.AddMacro(CreateMacro("First"));
        first.SequenceRepeatCount = 1;

        var second = _loadedMacroSession.AddMacro(CreateMacro("Second"));
        second.SequenceRepeatCount = 5;

        var third = _loadedMacroSession.AddMacro(CreateMacro("Third"));
        third.SequenceRepeatCount = 2;

        _loadedMacroSession.SelectedMacroItem = second;
        _loadedMacroSession.PlaybackMode = LoadedMacroPlaybackMode.SequentialCycle;

        var playedMacros = new List<MacroSequence>();
        var playedOptions = new List<PlaybackOptions>();
        _player.PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                playedMacros.Add(callInfo.ArgAt<MacroSequence>(0));
                playedOptions.Add(callInfo.ArgAt<PlaybackOptions>(1));
                return Task.CompletedTask;
            });

        await _viewModel.PlayMacroAsync();

        playedMacros.Select(macro => macro.Name).Should().ContainInOrder("Second", "Third", "First");
        playedMacros.Select(macro => macro.Id).Should().ContainInOrder(second.Macro.Id, third.Macro.Id, first.Macro.Id);
        playedOptions.Select(options => options.RepeatCount).Should().ContainInOrder(5, 2, 1);
        playedOptions.Select(options => options.Loop).Should().ContainInOrder(true, true, false);
        _loadedMacroSession.SelectedMacroItem.Should().BeSameAs(second);
    }

    [Fact]
    public async Task PlayMacroAsync_WhenSequentialCycleContainsInvalidLaterMacro_DoesNotStartPlayback()
    {
        var first = _loadedMacroSession.AddMacro(CreateMacro("First"));
        _loadedMacroSession.AddMacro(new MacroSequence { Name = "Broken" });
        _loadedMacroSession.SelectedMacroItem = first;
        _loadedMacroSession.PlaybackMode = LoadedMacroPlaybackMode.SequentialCycle;

        await _viewModel.PlayMacroAsync();

        await _player.DidNotReceive().PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>());
        _viewModel.IsPlaying.Should().BeFalse();
        _viewModel.PlaybackStatus.Should().Contain("Broken");
        _viewModel.PlaybackStatus.Should().Contain("has no events");
    }

    [Fact]
    public void CultureChanged_WhenIdle_RefreshesReadyStatusImmediately()
    {
        _localizationService["Playback_StatusReady"].Returns("[Playback_StatusReady:updated]");

        _localizationService.CultureChanged += Raise.Event<EventHandler>(_localizationService, EventArgs.Empty);

        _viewModel.PlaybackStatus.Should().Be("[Playback_StatusReady:updated]");
    }

    [Fact]
    public async Task PlayMacroAsync_WhenSequentialCycleModeHasSingleLoadedMacro_UsesSequenceRepeatCount()
    {
        var item = _loadedMacroSession.AddMacro(CreateMacro("Only"));
        item.SequenceRepeatCount = 4;
        _loadedMacroSession.PlaybackMode = LoadedMacroPlaybackMode.SequentialCycle;

        PlaybackOptions? capturedOptions = null;
        MacroSequence? capturedMacro = null;
        _player.PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedMacro = callInfo.ArgAt<MacroSequence>(0);
                capturedOptions = callInfo.ArgAt<PlaybackOptions>(1);
                return Task.CompletedTask;
            });

        await _viewModel.PlayMacroAsync();

        capturedMacro.Should().NotBeNull();
        capturedMacro!.Should().NotBeSameAs(item.Macro);
        capturedMacro.Id.Should().Be(item.Macro.Id);
        capturedMacro.Name.Should().Be(item.Macro.Name);
        capturedOptions.Should().NotBeNull();
        capturedOptions!.Loop.Should().BeTrue();
        capturedOptions.RepeatCount.Should().Be(4);
        _loadedMacroSession.SelectedMacroItem.Should().BeSameAs(item);
    }

    [Fact]
    public async Task PlayMacroAsync_WhenSequentialCycleStopped_RestoresOriginalSelection()
    {
        var first = _loadedMacroSession.AddMacro(CreateMacro("First"));
        var second = _loadedMacroSession.AddMacro(CreateMacro("Second"));
        var third = _loadedMacroSession.AddMacro(CreateMacro("Third"));

        _loadedMacroSession.SelectedMacroItem = second;
        _loadedMacroSession.PlaybackMode = LoadedMacroPlaybackMode.SequentialCycle;

        var invocationCount = 0;
        _player.PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                invocationCount++;
                if (invocationCount == 2)
                {
                    _viewModel.StopPlayback();
                }

                return Task.CompletedTask;
            });

        await _viewModel.PlayMacroAsync();

        _loadedMacroSession.SelectedMacroItem.Should().BeSameAs(second);
        _viewModel.PlaybackStatus.Should().Be("[Playback_StatusStopped]");
        _player.Received(1).Stop();
    }

    [Fact]
    public async Task StopPlayback_WhenTeardownStillRunning_KeepsStoppedStatus()
    {
        var macro = CreateMacro();
        _viewModel.SetMacro(macro);

        var playStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _player.PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                playStarted.TrySetResult(true);
                await allowCompletion.Task;
            });

        var playTask = _viewModel.PlayMacroAsync();
        await playStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        _viewModel.StopPlayback();
        InvokeNonPublicMethod(_viewModel, "OnStatusUpdateTimerTick", null, EventArgs.Empty);

        _viewModel.PlaybackStatus.Should().Be("[Playback_StatusStopped]");

        allowCompletion.TrySetResult(true);
        await playTask;
    }

    [Fact]
    public void TogglePause_WhenPlaying_PausesOrResumes()
    {
        _viewModel.GetType().GetProperty("IsPlaying")?.SetValue(_viewModel, true);

        _player.IsPaused.Returns(false);

        _viewModel.TogglePause();

        _player.Received(1).Pause();
        _viewModel.IsPaused.Should().BeTrue();
        _viewModel.PlaybackStatus.Should().Be("[Playback_StatusPaused]");

        _player.IsPaused.Returns(true);

        _viewModel.TogglePause();

        _player.Received(1).Resume();
        _viewModel.IsPaused.Should().BeFalse();
    }

    [Fact]
    public void StopPlayback_WhenPlaying_StopsPlayerAndSetsStatus()
    {
        _viewModel.GetType().GetProperty("IsPlaying")?.SetValue(_viewModel, true);

        _viewModel.StopPlayback();

        _player.Received(1).Stop();
        _viewModel.IsPlaying.Should().BeFalse();
        _viewModel.PlaybackStatus.Should().Be("[Playback_StatusStopped]");
    }

    [Fact]
    public async Task PlayMacroAsync_WhenPlayerThrows_SetsErrorStatusAndResetsPlaying()
    {
        var macro = CreateMacro();
        _viewModel.SetMacro(macro);
        _viewModel.CanPlayMacroExternal = true;
        _player.PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("simulator failed")));

        await _viewModel.PlayMacroAsync();

        _viewModel.IsPlaying.Should().BeFalse();
        _viewModel.PlaybackStatus.Should().Contain("[Playback_StatusError]");
        _viewModel.PlaybackStatus.Should().Contain("simulator failed");
    }

    [Fact]
    public void TogglePlayback_WhenCannotPlay_DoesNotInvokePlayer()
    {
        _viewModel.CanPlayMacroExternal = false;

        _viewModel.TogglePlayback();

        _player.DidNotReceive().Stop();
        _ = _player.DidNotReceive().PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void PlaybackSpeed_WhenSaveFails_RollsBackValue()
    {
        _settingsService.When(x => x.Save()).Do(_ => throw new InvalidOperationException("disk full"));

        _viewModel.PlaybackSpeed = 2.0;

        _viewModel.PlaybackSpeed.Should().Be(1.0);
        _settings.PlaybackSpeed.Should().Be(1.0);
    }

    private static void InvokeNonPublicMethod(object target, string methodName, params object?[]? args)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull();
        method!.Invoke(target, args);
    }

    private static MacroSequence CreateMacro(string name = "Test Macro")
    {
        return new MacroSequence
        {
            Name = name,
            Events = { new MacroEvent() }
        };
    }
}
