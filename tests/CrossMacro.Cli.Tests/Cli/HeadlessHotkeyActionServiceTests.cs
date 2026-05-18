using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Cli.Services;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Abstractions;
using CrossMacro.TestInfrastructure;
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
        var startRequested = new AsyncSignal();
        var stopRequested = new AsyncSignal();
        var recorder = Substitute.For<IMacroRecorder>();
        var runtimeContext = CreateRuntimeContext();
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
                startRequested.Signal();
                return Task.CompletedTask;
            });

        recorder.StopRecording().Returns(_ =>
        {
            isRecording = false;
            stopRequested.Signal();
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
        var service = new HeadlessHotkeyActionService(hotkeys, recorder, () => player, settings, runtimeContext);
        service.Start();

        hotkeys.ToggleRecordingRequested += Raise.Event<EventHandler>(hotkeys, EventArgs.Empty);
        await startRequested.WaitAsync(TimeSpan.FromSeconds(2));

        hotkeys.ToggleRecordingRequested += Raise.Event<EventHandler>(hotkeys, EventArgs.Empty);
        await stopRequested.WaitAsync(TimeSpan.FromSeconds(2));

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

        await service.DisposeAsync();
    }

    [Fact]
    public async Task PlaybackHotkeyToggle_WithNoRecordedMacro_DoesNotStartPlayer()
    {
        var hotkeys = Substitute.For<IGlobalHotkeyService>();
        var recorder = Substitute.For<IMacroRecorder>();
        var runtimeContext = CreateRuntimeContext();
        recorder.IsRecording.Returns(false);
        var settings = Substitute.For<ISettingsService>();
        settings.Current.Returns(new AppSettings());

        var playerFactoryInvoked = new AsyncSignal();
        var player = Substitute.For<IMacroPlayer>();
        var service = new HeadlessHotkeyActionService(hotkeys, recorder, () =>
        {
            playerFactoryInvoked.Signal();
            return player;
        }, settings, runtimeContext);
        service.Start();

        hotkeys.TogglePlaybackRequested += Raise.Event<EventHandler>(hotkeys, EventArgs.Empty);
        await Task.Yield();

        Assert.False(playerFactoryInvoked.IsSignaled);
        _ = player.DidNotReceive().PlayAsync(
            Arg.Any<MacroSequence>(),
            Arg.Any<PlaybackOptions>(),
            Arg.Any<CancellationToken>());

        await service.DisposeAsync();
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
        var startRequested = new AsyncSignal();
        var stopRequested = new AsyncSignal();
        var recorder = Substitute.For<IMacroRecorder>();
        var runtimeContext = CreateRuntimeContext();
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
                startRequested.Signal();
                return Task.Delay(Timeout.Infinite, CancellationToken.None);
            });

        recorder.StopRecording().Returns(_ =>
        {
            isRecording = false;
            stopRequested.Signal();
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
        var service = new HeadlessHotkeyActionService(hotkeys, recorder, () => player, settings, runtimeContext);
        service.Start();

        hotkeys.ToggleRecordingRequested += Raise.Event<EventHandler>(hotkeys, EventArgs.Empty);
        await startRequested.WaitAsync(TimeSpan.FromSeconds(2));

        hotkeys.ToggleRecordingRequested += Raise.Event<EventHandler>(hotkeys, EventArgs.Empty);
        await stopRequested.WaitAsync(TimeSpan.FromSeconds(2));

        recorder.Received(1).StopRecording();
        await service.DisposeAsync();
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
        var startRequested = new AsyncSignal();
        var stopRequested = new AsyncSignal();
        var recorder = Substitute.For<IMacroRecorder>();
        var runtimeContext = CreateRuntimeContext();
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
                startRequested.Signal();
                return Task.CompletedTask;
            });

        recorder.StopRecording().Returns(_ =>
        {
            isRecording = false;
            stopRequested.Signal();
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
        var countdownDelay = new ControlledDelay();
        var playStarted = new AsyncSignal();
        var stopCalled = new AsyncSignal();
        PlaybackOptions? capturedOptions = null;
        player.PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                capturedOptions = ci.ArgAt<PlaybackOptions>(1);
                var token = ci.ArgAt<CancellationToken>(2);
                playStarted.Signal();
                return Task.Delay(Timeout.Infinite, token);
            });
        player.When(x => x.Stop()).Do(_ => stopCalled.Signal());

        var service = new HeadlessHotkeyActionService(hotkeys, recorder, () => player, settings, runtimeContext, countdownDelay.DelayAsync);
        service.Start();

        hotkeys.ToggleRecordingRequested += Raise.Event<EventHandler>(hotkeys, EventArgs.Empty);
        await startRequested.WaitAsync(TimeSpan.FromSeconds(2));
        hotkeys.ToggleRecordingRequested += Raise.Event<EventHandler>(hotkeys, EventArgs.Empty);
        await stopRequested.WaitAsync(TimeSpan.FromSeconds(2));

        hotkeys.TogglePlaybackRequested += Raise.Event<EventHandler>(hotkeys, EventArgs.Empty);
        await playStarted.WaitAsync(TimeSpan.FromSeconds(2));

        hotkeys.TogglePlaybackRequested += Raise.Event<EventHandler>(hotkeys, EventArgs.Empty);
        await stopCalled.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.NotNull(capturedOptions);
        Assert.Equal(1.5, capturedOptions!.SpeedMultiplier);
        Assert.True(capturedOptions.Loop);
        Assert.Equal(3, capturedOptions.RepeatCount);
        Assert.Equal(10, capturedOptions.RepeatDelayMs);

        await service.DisposeAsync();
    }

    [Fact]
    public async Task PlaybackHotkeyToggle_ForwardsRandomLoopDelaySettings()
    {
        var hotkeys = Substitute.For<IGlobalHotkeyService>();
        var settings = Substitute.For<ISettingsService>();
        settings.Current.Returns(new AppSettings
        {
            IsMouseRecordingEnabled = true,
            IsKeyboardRecordingEnabled = true,
            IsLooping = true,
            LoopCount = 2,
            UseRandomLoopDelay = true,
            LoopDelayMinMs = 40,
            LoopDelayMaxMs = 80
        });

        var isRecording = false;
        var startRequested = new AsyncSignal();
        var stopRequested = new AsyncSignal();
        var recorder = Substitute.For<IMacroRecorder>();
        var runtimeContext = CreateRuntimeContext();
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
                startRequested.Signal();
                return Task.CompletedTask;
            });

        recorder.StopRecording().Returns(_ =>
        {
            isRecording = false;
            stopRequested.Signal();
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
        var playStarted = new AsyncSignal();
        var stopCalled = new AsyncSignal();
        PlaybackOptions? capturedOptions = null;
        player.PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                capturedOptions = ci.ArgAt<PlaybackOptions>(1);
                var token = ci.ArgAt<CancellationToken>(2);
                playStarted.Signal();
                return Task.Delay(Timeout.Infinite, token);
            });
        player.When(x => x.Stop()).Do(_ => stopCalled.Signal());

        var service = new HeadlessHotkeyActionService(hotkeys, recorder, () => player, settings, runtimeContext);
        service.Start();

        hotkeys.ToggleRecordingRequested += Raise.Event<EventHandler>(hotkeys, EventArgs.Empty);
        await startRequested.WaitAsync(TimeSpan.FromSeconds(2));
        hotkeys.ToggleRecordingRequested += Raise.Event<EventHandler>(hotkeys, EventArgs.Empty);
        await stopRequested.WaitAsync(TimeSpan.FromSeconds(2));

        hotkeys.TogglePlaybackRequested += Raise.Event<EventHandler>(hotkeys, EventArgs.Empty);
        await playStarted.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.NotNull(capturedOptions);
        Assert.True(capturedOptions!.UseRandomRepeatDelay);
        Assert.Equal(40, capturedOptions.RepeatDelayMinMs);
        Assert.Equal(80, capturedOptions.RepeatDelayMaxMs);

        hotkeys.TogglePlaybackRequested += Raise.Event<EventHandler>(hotkeys, EventArgs.Empty);
        await stopCalled.WaitAsync(TimeSpan.FromSeconds(2));

        await service.DisposeAsync();
    }

    [Fact]
    public async Task StopAsync_WaitsForActivePlaybackTaskToFinishCleanup()
    {
        var hotkeys = Substitute.For<IGlobalHotkeyService>();
        var settings = Substitute.For<ISettingsService>();
        settings.Current.Returns(new AppSettings
        {
            IsMouseRecordingEnabled = true,
            IsKeyboardRecordingEnabled = true
        });

        var isRecording = false;
        var startRequested = new AsyncSignal();
        var stopRequested = new AsyncSignal();
        var recorder = Substitute.For<IMacroRecorder>();
        var runtimeContext = CreateRuntimeContext();
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
                startRequested.Signal();
                return Task.CompletedTask;
            });
        recorder.StopRecording().Returns(_ =>
        {
            isRecording = false;
            stopRequested.Signal();
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

        var player = new BlockingPlayer();
        var service = new HeadlessHotkeyActionService(hotkeys, recorder, () => player, settings, runtimeContext);
        service.Start();

        hotkeys.ToggleRecordingRequested += Raise.Event<EventHandler>(hotkeys, EventArgs.Empty);
        await startRequested.WaitAsync(TimeSpan.FromSeconds(2));
        hotkeys.ToggleRecordingRequested += Raise.Event<EventHandler>(hotkeys, EventArgs.Empty);
        await stopRequested.WaitAsync(TimeSpan.FromSeconds(2));

        hotkeys.TogglePlaybackRequested += Raise.Event<EventHandler>(hotkeys, EventArgs.Empty);
        await player.PlayStarted.WaitAsync(TimeSpan.FromSeconds(2));

        await service.StopAsync();

        Assert.True(player.PlayCompleted.IsSignaled);
        Assert.True(player.DisposeCalled);

        await service.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_AfterStop_WaitsForPendingStopCleanupBeforeDisposingGate()
    {
        var hotkeys = Substitute.For<IGlobalHotkeyService>();
        var settings = Substitute.For<ISettingsService>();
        settings.Current.Returns(new AppSettings
        {
            IsMouseRecordingEnabled = true,
            IsKeyboardRecordingEnabled = true
        });

        var isRecording = false;
        var startRequested = new AsyncSignal();
        var stopRequested = new AsyncSignal();
        var recorder = Substitute.For<IMacroRecorder>();
        var runtimeContext = CreateRuntimeContext();
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
                startRequested.Signal();
                return Task.CompletedTask;
            });
        recorder.StopRecording().Returns(_ =>
        {
            isRecording = false;
            stopRequested.Signal();
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

        var player = new GatedCleanupPlayer();
        var service = new HeadlessHotkeyActionService(hotkeys, recorder, () => player, settings, runtimeContext);
        service.Start();

        hotkeys.ToggleRecordingRequested += Raise.Event<EventHandler>(hotkeys, EventArgs.Empty);
        await startRequested.WaitAsync(TimeSpan.FromSeconds(2));
        hotkeys.ToggleRecordingRequested += Raise.Event<EventHandler>(hotkeys, EventArgs.Empty);
        await stopRequested.WaitAsync(TimeSpan.FromSeconds(2));

        hotkeys.TogglePlaybackRequested += Raise.Event<EventHandler>(hotkeys, EventArgs.Empty);
        await player.PlayStarted.WaitAsync(TimeSpan.FromSeconds(2));

        service.Stop();
        await player.StopCalled.WaitAsync(TimeSpan.FromSeconds(2));
        await player.CleanupEntered.WaitAsync(TimeSpan.FromSeconds(2));

        var disposeTask = service.DisposeAsync().AsTask();

        Assert.False(disposeTask.IsCompleted);
        Assert.False(player.DisposeCalled);

        player.AllowCleanupToComplete();
        await disposeTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(player.PlayCompleted.IsSignaled);
        Assert.True(player.DisposeCalled);
    }

    [Fact]
    public async Task RecordingHotkeyToggle_WhenRuntimeDoesNotSupportForceRelative_DoesNotForwardForceRelative()
    {
        var hotkeys = Substitute.For<IGlobalHotkeyService>();
        var settings = Substitute.For<ISettingsService>();
        settings.Current.Returns(new AppSettings
        {
            IsMouseRecordingEnabled = true,
            IsKeyboardRecordingEnabled = true,
            ForceRelativeCoordinates = true,
            SkipInitialZeroZero = true
        });

        var recorder = Substitute.For<IMacroRecorder>();
        recorder.IsRecording.Returns(false);
        var runtimeContext = CreateRuntimeContext(isLinux: false, isWindows: false, isMacOS: false);
        var player = Substitute.For<IMacroPlayer>();
        var service = new HeadlessHotkeyActionService(hotkeys, recorder, () => player, settings, runtimeContext);
        service.Start();

        hotkeys.ToggleRecordingRequested += Raise.Event<EventHandler>(hotkeys, EventArgs.Empty);
        await Task.Yield();

        await recorder.Received(1).StartRecordingAsync(
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<IEnumerable<int>>(),
            false,
            false,
            Arg.Any<CancellationToken>());

        await service.DisposeAsync();
    }

    [Fact]
    public async Task RecordingHotkeyToggle_WhenRuntimeIsMacOS_ForwardsForceRelative()
    {
        var hotkeys = Substitute.For<IGlobalHotkeyService>();
        var settings = Substitute.For<ISettingsService>();
        settings.Current.Returns(new AppSettings
        {
            IsMouseRecordingEnabled = true,
            IsKeyboardRecordingEnabled = true,
            ForceRelativeCoordinates = true,
            SkipInitialZeroZero = true
        });

        var recorder = Substitute.For<IMacroRecorder>();
        recorder.IsRecording.Returns(false);
        var runtimeContext = CreateRuntimeContext(isLinux: false, isWindows: false, isMacOS: true);
        var player = Substitute.For<IMacroPlayer>();
        var service = new HeadlessHotkeyActionService(hotkeys, recorder, () => player, settings, runtimeContext);
        service.Start();

        hotkeys.ToggleRecordingRequested += Raise.Event<EventHandler>(hotkeys, EventArgs.Empty);
        await Task.Yield();

        await recorder.Received(1).StartRecordingAsync(
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<IEnumerable<int>>(),
            true,
            true,
            Arg.Any<CancellationToken>());

        await service.DisposeAsync();
    }

    private static IRuntimeContext CreateRuntimeContext(bool isLinux = true, bool isWindows = false, bool isMacOS = false)
    {
        var runtimeContext = Substitute.For<IRuntimeContext>();
        runtimeContext.IsLinux.Returns(isLinux);
        runtimeContext.IsWindows.Returns(isWindows);
        runtimeContext.IsMacOS.Returns(isMacOS);
        return runtimeContext;
    }

    private sealed class ControlledDelay
    {
        private readonly object _sync = new();
        private readonly Queue<DelayRequest> _requests = new();
        private readonly AsyncSignal _requestArrived = new();

        public Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
        {
            var completionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var request = new DelayRequest(duration, completionSource);

            CancellationTokenRegistration cancellationRegistration = default;
            if (cancellationToken.CanBeCanceled)
            {
                cancellationRegistration = cancellationToken.Register(static state =>
                {
                    ((TaskCompletionSource)state!).TrySetCanceled();
                }, completionSource);

                _ = completionSource.Task.ContinueWith(
                    static (_, state) => ((CancellationTokenRegistration)state!).Dispose(),
                    cancellationRegistration,
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }

            lock (_sync)
            {
                _requests.Enqueue(request);
                _requestArrived.Signal();
            }

            return completionSource.Task;
        }

        public async Task<DelayRequest> WaitForNextRequestAsync(TimeSpan timeout)
        {
            while (true)
            {
                lock (_sync)
                {
                    if (_requests.Count > 0)
                    {
                        var request = _requests.Dequeue();
                        if (_requests.Count == 0)
                        {
                            _requestArrived.Reset();
                        }

                        return request;
                    }

                    _requestArrived.Reset();
                }

                await _requestArrived.WaitAsync(timeout);
            }
        }
    }

    private sealed class DelayRequest(TimeSpan duration, TaskCompletionSource completionSource)
    {
        public TimeSpan Duration { get; } = duration;

        public void Complete()
        {
            completionSource.TrySetResult();
        }
    }

    private sealed class BlockingPlayer : IMacroPlayer
    {
        public AsyncSignal PlayStarted { get; } = new();
        public AsyncSignal PlayCompleted { get; } = new();
        public bool DisposeCalled { get; private set; }

        public bool IsPaused => false;
        public int CurrentLoop => 1;
        public int TotalLoops => 1;
        public bool IsWaitingBetweenLoops => false;

        public async Task PlayAsync(MacroSequence macro, PlaybackOptions? options = null, CancellationToken cancellationToken = default)
        {
            PlayStarted.Signal();

            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                PlayCompleted.Signal();
            }
        }

        public void Stop()
        {
        }

        public void Pause()
        {
        }

        public void Resume()
        {
        }

        public void Dispose()
        {
            DisposeCalled = true;
        }
    }

    private sealed class GatedCleanupPlayer : IMacroPlayer
    {
        private readonly TaskCompletionSource _allowCleanupToComplete = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public AsyncSignal PlayStarted { get; } = new();
        public AsyncSignal StopCalled { get; } = new();
        public AsyncSignal CleanupEntered { get; } = new();
        public AsyncSignal PlayCompleted { get; } = new();
        public bool DisposeCalled { get; private set; }

        public bool IsPaused => false;
        public int CurrentLoop => 1;
        public int TotalLoops => 1;
        public bool IsWaitingBetweenLoops => false;

        public async Task PlayAsync(MacroSequence macro, PlaybackOptions? options = null, CancellationToken cancellationToken = default)
        {
            PlayStarted.Signal();

            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                CleanupEntered.Signal();
                await _allowCleanupToComplete.Task;
                PlayCompleted.Signal();
            }
        }

        public void AllowCleanupToComplete()
        {
            _allowCleanupToComplete.SetResult();
        }

        public void Stop()
        {
            StopCalled.Signal();
        }

        public void Pause()
        {
        }

        public void Resume()
        {
        }

        public void Dispose()
        {
            DisposeCalled = true;
        }
    }
}
