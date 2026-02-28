using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Cli.Services;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using NSubstitute;

namespace CrossMacro.Cli.Tests;

public class HeadlessHotkeyActionServiceTests
{
    [Fact]
    public async Task RecordingHotkeyToggle_StartsThenStopsRecording()
    {
        var hotkeys = Substitute.For<IGlobalHotkeyService>();
        var settings = Substitute.For<ISettingsService>();
        settings.Current.Returns(new AppSettings
        {
            IsMouseRecordingEnabled = true,
            IsKeyboardRecordingEnabled = true
        });

        var isRecording = false;
        var recorder = Substitute.For<IMacroRecorder>();
        recorder.IsRecording.Returns(_ => isRecording);
        recorder.StartRecordingAsync(
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<IEnumerable<int>>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                isRecording = true;
                return Task.CompletedTask;
            });

        recorder.StopRecording().Returns(_ =>
        {
            isRecording = false;
            return new MacroSequence
            {
                Events =
                [
                    new MacroEvent
                    {
                        Type = EventType.KeyPress,
                        KeyCode = 30
                    }
                ]
            };
        });

        var player = Substitute.For<IMacroPlayer>();
        var service = new HeadlessHotkeyActionService(hotkeys, recorder, () => player, settings);
        service.Start();

        hotkeys.ToggleRecordingRequested += Raise.Event<EventHandler>(hotkeys, EventArgs.Empty);
        await WaitUntilAsync(() => isRecording);

        hotkeys.ToggleRecordingRequested += Raise.Event<EventHandler>(hotkeys, EventArgs.Empty);
        await WaitUntilAsync(() => !isRecording);

        _ = recorder.Received(1).StartRecordingAsync(
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<IEnumerable<int>>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());

        recorder.Received(1).StopRecording();
        hotkeys.Received(1).SetPlaybackPauseHotkeysEnabled(false);
        hotkeys.Received(1).SetPlaybackPauseHotkeysEnabled(true);

        service.Dispose();
    }

    [Fact]
    public async Task PlaybackHotkeyToggle_WithNoRecordedMacro_DoesNotStartPlayer()
    {
        var hotkeys = Substitute.For<IGlobalHotkeyService>();
        var recorder = Substitute.For<IMacroRecorder>();
        recorder.IsRecording.Returns(false);
        var settings = Substitute.For<ISettingsService>();
        settings.Current.Returns(new AppSettings());

        var player = Substitute.For<IMacroPlayer>();
        var service = new HeadlessHotkeyActionService(hotkeys, recorder, () => player, settings);
        service.Start();

        hotkeys.TogglePlaybackRequested += Raise.Event<EventHandler>(hotkeys, EventArgs.Empty);
        await Task.Delay(50);

        _ = player.DidNotReceive().PlayAsync(
            Arg.Any<MacroSequence>(),
            Arg.Any<PlaybackOptions>(),
            Arg.Any<CancellationToken>());

        service.Dispose();
    }

    [Fact]
    public async Task RecordingHotkeyToggle_StopsEvenWhenStartTaskDoesNotComplete()
    {
        var hotkeys = Substitute.For<IGlobalHotkeyService>();
        var settings = Substitute.For<ISettingsService>();
        settings.Current.Returns(new AppSettings
        {
            IsMouseRecordingEnabled = true,
            IsKeyboardRecordingEnabled = true
        });

        var isRecording = false;
        var recorder = Substitute.For<IMacroRecorder>();
        recorder.IsRecording.Returns(_ => isRecording);
        recorder.StartRecordingAsync(
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<IEnumerable<int>>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                isRecording = true;
                return Task.Delay(Timeout.Infinite, CancellationToken.None);
            });

        recorder.StopRecording().Returns(_ =>
        {
            isRecording = false;
            return new MacroSequence
            {
                Events =
                [
                    new MacroEvent
                    {
                        Type = EventType.KeyPress,
                        KeyCode = 30
                    }
                ]
            };
        });

        var player = Substitute.For<IMacroPlayer>();
        var service = new HeadlessHotkeyActionService(hotkeys, recorder, () => player, settings);
        service.Start();

        hotkeys.ToggleRecordingRequested += Raise.Event<EventHandler>(hotkeys, EventArgs.Empty);
        await WaitUntilAsync(() => isRecording);

        hotkeys.ToggleRecordingRequested += Raise.Event<EventHandler>(hotkeys, EventArgs.Empty);
        await WaitUntilAsync(() => !isRecording);

        recorder.Received(1).StopRecording();
        service.Dispose();
    }

    [Fact]
    public async Task PlaybackHotkeyToggle_StartsAndStopsPlayback_ForLastRecordedMacro()
    {
        var hotkeys = Substitute.For<IGlobalHotkeyService>();
        var settings = Substitute.For<ISettingsService>();
        settings.Current.Returns(new AppSettings
        {
            IsMouseRecordingEnabled = true,
            IsKeyboardRecordingEnabled = true,
            PlaybackSpeed = 1.5,
            IsLooping = true,
            LoopCount = 3,
            LoopDelayMs = 10
        });

        var isRecording = false;
        var recorder = Substitute.For<IMacroRecorder>();
        recorder.IsRecording.Returns(_ => isRecording);
        recorder.StartRecordingAsync(
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<IEnumerable<int>>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                isRecording = true;
                return Task.CompletedTask;
            });

        recorder.StopRecording().Returns(_ =>
        {
            isRecording = false;
            return new MacroSequence
            {
                Events =
                [
                    new MacroEvent
                    {
                        Type = EventType.Click,
                        Button = MouseButton.Left
                    }
                ]
            };
        });

        var player = Substitute.For<IMacroPlayer>();
        var playStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        PlaybackOptions? capturedOptions = null;
        player.PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                capturedOptions = ci.ArgAt<PlaybackOptions>(1);
                var token = ci.ArgAt<CancellationToken>(2);
                playStarted.TrySetResult(true);
                return Task.Delay(Timeout.Infinite, token);
            });

        var service = new HeadlessHotkeyActionService(hotkeys, recorder, () => player, settings);
        service.Start();

        hotkeys.ToggleRecordingRequested += Raise.Event<EventHandler>(hotkeys, EventArgs.Empty);
        await WaitUntilAsync(() => isRecording);
        hotkeys.ToggleRecordingRequested += Raise.Event<EventHandler>(hotkeys, EventArgs.Empty);
        await WaitUntilAsync(() => !isRecording);

        hotkeys.TogglePlaybackRequested += Raise.Event<EventHandler>(hotkeys, EventArgs.Empty);
        await playStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        hotkeys.TogglePlaybackRequested += Raise.Event<EventHandler>(hotkeys, EventArgs.Empty);
        await WaitUntilAsync(() =>
        {
            try
            {
                player.Received(1).Stop();
                return true;
            }
            catch
            {
                return false;
            }
        });

        Assert.NotNull(capturedOptions);
        Assert.Equal(1.5, capturedOptions!.SpeedMultiplier);
        Assert.True(capturedOptions.Loop);
        Assert.Equal(3, capturedOptions.RepeatCount);
        Assert.Equal(10, capturedOptions.RepeatDelayMs);

        service.Dispose();
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 2000)
    {
        var started = DateTime.UtcNow;
        while (!condition())
        {
            if ((DateTime.UtcNow - started).TotalMilliseconds > timeoutMs)
            {
                throw new TimeoutException("Condition was not satisfied in time.");
            }

            await Task.Delay(20);
        }
    }
}
