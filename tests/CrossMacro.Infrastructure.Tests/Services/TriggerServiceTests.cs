
namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class TriggerServiceTests : IDisposable
{
    private static readonly CancellationToken NonCancelableToken = new(canceled: false);
    private readonly IWindowManager _windowManager;
    private readonly IProfileSwitchRequests _profileSwitchRequests;
    private readonly IMacroFileManager _macroFileManager;
    private readonly IMacroPlayer _macroPlayer;
    private readonly TriggerService _service;
    private readonly string _testRootDirectory;
    private readonly string _triggersFilePath;

    public TriggerServiceTests()
    {
        _testRootDirectory = Path.Combine(
            Path.GetTempPath(),
            "crossmacro-tests",
            nameof(TriggerServiceTests),
            Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(_testRootDirectory);
        _triggersFilePath = Path.Combine(_testRootDirectory, "triggers.json");

        _windowManager = Substitute.For<IWindowManager>();
        _ = _windowManager.IsSupported.Returns(returnThis: true);
        _profileSwitchRequests = Substitute.For<IProfileSwitchRequests>();
        _macroFileManager = Substitute.For<IMacroFileManager>();
        _macroPlayer = Substitute.For<IMacroPlayer>();

        var testSynchronizationContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(syncContext: null);
        try
        {
            _service = new TriggerService(
                _windowManager,
                _profileSwitchRequests,
                _macroFileManager,
                () => _macroPlayer,
                _triggersFilePath);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(testSynchronizationContext);
        }
    }

    public void Dispose()
    {
        try { _service.Dispose(); } catch { }
        try
        {
            if (Directory.Exists(_testRootDirectory))
            {
                Directory.Delete(_testRootDirectory, recursive: true);
            }
        }
        catch { }
    }

    [Fact]
    public void Start_SetsIsMonitoringTrue()
    {
        _service.Start();
        _ = _service.IsMonitoring.Should().BeTrue();
    }

    [Fact]
    public void Stop_SetsIsMonitoringFalse()
    {
        _service.Start();
        _service.StopMonitoring();
        _ = _service.IsMonitoring.Should().BeFalse();
    }

    [Fact]
    public async Task Stop_ExposesCompletionAndIsIdempotent()
    {
        _service.Start();
        _service.StopMonitoring();
        _service.StopMonitoring();

        await _service.Completion.WaitAsync(TimeSpan.FromSeconds(2));

        _ = _service.Completion.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task StopAsync_ConcurrentRestartKeepsReplacementMonitorAlive()
    {
        var oldMonitorStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var replacementStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var replacementPollGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pollCount = 0;

        _ = _windowManager.GetActiveWindowAsync(Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var token = callInfo.Arg<CancellationToken>();
                if (Interlocked.Increment(ref pollCount) is 1)
                {
                    _ = token.Register(() =>
                    {
                        _service.Start();
                        replacementStarted.SetResult();
                    });
                    oldMonitorStarted.SetResult();
                }
                else
                {
                    await replacementPollGate.Task.WaitAsync(token);
                }

                return (WindowInfo?)new WindowInfo();
            });

        _service.Start();
        await oldMonitorStarted.Task;

        var stopTask = _service.StopAsync();
        await replacementStarted.Task;
        await stopTask;

        _ = _service.IsMonitoring.Should().BeTrue();

        replacementPollGate.SetResult();
        await _service.StopAsync();
        _ = _service.IsMonitoring.Should().BeFalse();
    }

    [Fact]
    public async Task StopAsync_ConcurrentRestartPreservesReplacementMatchingState()
    {
        var oldMonitorStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var replacementStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var replacementStateWritten = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldPollGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pollCount = 0;
        var task = new TriggerTask
        {
            Value = "firefox",
            Field = TriggerField.WindowClass,
            MatchMode = TriggerMatchMode.Equals,
            Action = TriggerOperation.SwitchProfile,
            TargetProfileId = "work",
            FireMode = TriggerFireMode.OnceOnChange,
            IsEnabled = true,
        };
        _service.AddTask(task);
        _service.TriggerFired += (_, _) => replacementStateWritten.TrySetResult();

        _ = _windowManager.GetActiveWindowAsync(Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var token = callInfo.Arg<CancellationToken>();
                if (Interlocked.Increment(ref pollCount) is 1)
                {
                    _ = token.Register(() =>
                    {
                        _service.Start();
                        replacementStarted.SetResult();
                    });
                    oldMonitorStarted.SetResult();
                    await oldPollGate.Task;
                }

                return new WindowInfo { Class = "firefox" };
            });

        _service.Start();
        await oldMonitorStarted.Task;

        var stopTask = _service.StopAsync();
        await replacementStarted.Task;
        await replacementStateWritten.Task;
        _ = stopTask.IsCompleted.Should().BeFalse();

        oldPollGate.SetResult();
        await stopTask;

        await _service.PollOnceAsync(CancellationToken.None);

        await _profileSwitchRequests.Received(1).RequestSwitchAsync("work");

        await _service.StopAsync();
    }

    [Fact]
    public async Task StopAsync_WaitsForCanceledMonitorToExit()
    {
        var oldMonitorStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var monitorExitGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var monitorToken = CancellationToken.None;

        _ = _windowManager.GetActiveWindowAsync(Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var token = callInfo.Arg<CancellationToken>();
                monitorToken = token;
                _ = token.Register(() => cancellationObserved.TrySetResult());
                _ = oldMonitorStarted.TrySetResult();
                await monitorExitGate.Task;
                return (WindowInfo?)new WindowInfo();
            });

        _service.Start();
        await oldMonitorStarted.Task;

        var stopTask = _service.StopAsync(monitorToken);
        await cancellationObserved.Task;

        _ = stopTask.IsCompleted.Should().BeFalse();

        monitorExitGate.SetResult();
        await stopTask;
        _ = _service.IsMonitoring.Should().BeFalse();
    }

    [Fact]
    public async Task Dispose_WaitsForCanceledMonitorToExit_AndIsIdempotent()
    {
        var monitorStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var monitorExitGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _ = _windowManager.GetActiveWindowAsync(Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var token = callInfo.Arg<CancellationToken>();
                _ = token.Register(() => cancellationObserved.TrySetResult());
                _ = monitorStarted.TrySetResult();
                await monitorExitGate.Task;
                return (WindowInfo?)new WindowInfo();
            });

        _service.Start();
        await monitorStarted.Task;

        var disposeTask = Task.Run(_service.Dispose);
        await cancellationObserved.Task;

        _ = disposeTask.IsCompleted.Should().BeFalse();
        monitorExitGate.SetResult();
        await disposeTask;

        _ = _service.IsMonitoring.Should().BeFalse();
        _service.Dispose();
    }

    [Fact]
    public void AddTask_AddsToCollection()
    {
        var task = new TriggerTask();
        _service.AddTask(task);
        _ = _service.Tasks.Should().Contain(task);
    }

    [Fact]
    public void RemoveTask_RemovesFromCollection()
    {
        var task = new TriggerTask();
        _service.AddTask(task);
        _service.RemoveTask(task.Id);
        _ = _service.Tasks.Should().NotContain(task);
    }

    [Fact]
    public void SetTaskEnabled_DisablesTask()
    {
        var task = new TriggerTask
        {
            Value = "firefox",
            Action = TriggerOperation.SwitchProfile,
            TargetProfileId = "gaming",
        };
        _service.AddTask(task);
        task.IsEnabled = true;

        _service.SetTaskEnabled(task.Id, enabled: false);

        _ = task.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task PollOnce_WithUnsupportedWindowManager_DoesNotAttemptWindowQuery()
    {
        _ = _windowManager.IsSupported.Returns(returnThis: false);

        await _service.PollOnceAsync(CancellationToken.None);

        _ = await _windowManager.DidNotReceive()
            .GetActiveWindowAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PollOnce_WithMatchingWindow_FiresSwitchProfile()
    {
        var task = new TriggerTask
        {
            Value = "firefox",
            Field = TriggerField.WindowClass,
            MatchMode = TriggerMatchMode.Contains,
            Action = TriggerOperation.SwitchProfile,
            TargetProfileId = "gaming",
            FireMode = TriggerFireMode.EveryMatch,
            IsEnabled = true,
        };
        _service.AddTask(task);

        _ = _windowManager.GetActiveWindowAsync(Arg.Any<CancellationToken>())
            .Returns(new WindowInfo { Class = "firefox", Title = "Firefox" });

        await _service.PollOnceAsync(CancellationToken.None);

        await _profileSwitchRequests.Received().RequestSwitchAsync("gaming");
        _ = task.LastTriggeredTime.Should().NotBeNull();
        _ = task.LastStatus.Should().Contain("gaming");
    }

    [Fact]
    public async Task PollOnce_OnceOnChange_FiresOnlyOnceWhileMatchPersists()
    {
        var task = new TriggerTask
        {
            Value = "Code",
            Field = TriggerField.WindowTitle,
            MatchMode = TriggerMatchMode.Contains,
            Action = TriggerOperation.SwitchProfile,
            TargetProfileId = "dev",
            FireMode = TriggerFireMode.OnceOnChange,
            IsEnabled = true,
        };
        _service.AddTask(task);

        _ = _windowManager.GetActiveWindowAsync(Arg.Any<CancellationToken>())
            .Returns(new WindowInfo { Class = "Code", Title = "main.rs - VS Code" });

        // First poll: match becomes true → fires
        await _service.PollOnceAsync(CancellationToken.None);
        // Second poll: match still true → should NOT fire again
        await _service.PollOnceAsync(CancellationToken.None);

        await _profileSwitchRequests.Received(1).RequestSwitchAsync("dev");
    }

    [Fact]
    public async Task PollOnce_WithMatchingProcessName_FiresSwitchProfile()
    {
        var task = new TriggerTask
        {
            Value = "firefox",
            Field = TriggerField.ProcessName,
            MatchMode = TriggerMatchMode.Equals,
            Action = TriggerOperation.SwitchProfile,
            TargetProfileId = "gaming",
            FireMode = TriggerFireMode.EveryMatch,
            IsEnabled = true,
        };
        _service.AddTask(task);

        _ = _windowManager.GetActiveWindowAsync(Arg.Any<CancellationToken>())
            .Returns(new WindowInfo { ProcessName = "firefox", Class = "Firefox", Title = "Firefox" });

        await _service.PollOnceAsync(CancellationToken.None);

        await _profileSwitchRequests.Received().RequestSwitchAsync("gaming");
        _ = task.LastTriggeredTime.Should().NotBeNull();
        _ = task.LastStatus.Should().Contain("gaming");
    }

    [Fact]
    public async Task PollOnce_OnceOnChange_RefiresAfterMatchBreaksAndResumes()
    {
        var task = new TriggerTask
        {
            Value = "Code",
            Field = TriggerField.WindowTitle,
            MatchMode = TriggerMatchMode.Contains,
            Action = TriggerOperation.SwitchProfile,
            TargetProfileId = "dev",
            FireMode = TriggerFireMode.OnceOnChange,
            IsEnabled = true,
        };
        _service.AddTask(task);

        // Poll 1: matching → fires
        _ = _windowManager.GetActiveWindowAsync(Arg.Any<CancellationToken>())
            .Returns(new WindowInfo { Class = "Code", Title = "main.rs - VS Code" });
        await _service.PollOnceAsync(CancellationToken.None);

        // Poll 2: no longer matching → no fire, state resets
        _ = _windowManager.GetActiveWindowAsync(Arg.Any<CancellationToken>())
            .Returns(new WindowInfo { Class = "terminal", Title = "Terminal" });
        await _service.PollOnceAsync(CancellationToken.None);

        // Poll 3: matching again → should fire again
        _ = _windowManager.GetActiveWindowAsync(Arg.Any<CancellationToken>())
            .Returns(new WindowInfo { Class = "Code", Title = "main.rs - VS Code" });
        await _service.PollOnceAsync(CancellationToken.None);

        await _profileSwitchRequests.Received(2).RequestSwitchAsync("dev");
    }

    [Fact]
    public async Task PollOnce_NonMatchingWindow_DoesNotFire()
    {
        var task = new TriggerTask
        {
            Value = "firefox",
            Field = TriggerField.WindowClass,
            MatchMode = TriggerMatchMode.Contains,
            Action = TriggerOperation.SwitchProfile,
            TargetProfileId = "gaming",
            FireMode = TriggerFireMode.EveryMatch,
            IsEnabled = true,
        };
        _service.AddTask(task);

        _ = _windowManager.GetActiveWindowAsync(Arg.Any<CancellationToken>())
            .Returns(new WindowInfo { Class = "terminal", Title = "Terminal" });

        await _service.PollOnceAsync(CancellationToken.None);

        await _profileSwitchRequests.DidNotReceive().RequestSwitchAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task PollOnce_WorkspaceCondition_FiresSwitchProfile_WhenWorkspaceMatches()
    {
        var task = new TriggerTask
        {
            Value = "dev",
            Field = TriggerField.Workspace,
            MatchMode = TriggerMatchMode.Equals,
            Action = TriggerOperation.SwitchProfile,
            TargetProfileId = "work",
            FireMode = TriggerFireMode.EveryMatch,
            IsEnabled = true,
        };
        _service.AddTask(task);

        _ = _windowManager.GetActiveWorkspaceAsync(Arg.Any<CancellationToken>())
            .Returns("dev");

        await _service.PollOnceAsync(CancellationToken.None);

        await _profileSwitchRequests.Received(1).RequestSwitchAsync("work");
        _ = task.LastStatus.Should().Contain("work");
    }

    [Fact]
    public async Task PollOnce_WorkspaceCondition_DoesNotFire_WhenWorkspaceMismatch()
    {
        var task = new TriggerTask
        {
            Value = "dev",
            Field = TriggerField.Workspace,
            MatchMode = TriggerMatchMode.Equals,
            Action = TriggerOperation.SwitchProfile,
            TargetProfileId = "work",
            FireMode = TriggerFireMode.EveryMatch,
            IsEnabled = true,
        };
        _service.AddTask(task);

        _ = _windowManager.GetActiveWorkspaceAsync(Arg.Any<CancellationToken>())
            .Returns("personal");

        await _service.PollOnceAsync(CancellationToken.None);

        await _profileSwitchRequests.DidNotReceive().RequestSwitchAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task PollOnce_RunMacroAction_LoadsAndPlaysMacro()
    {
        var macroPath = Path.Combine(_testRootDirectory, "demo.macro");
        await File.WriteAllTextAsync(macroPath, "{}", NonCancelableToken);
        var macro = new MacroSequence();

        _ = _macroFileManager.LoadAsync(macroPath).Returns(macro);

        var task = new TriggerTask
        {
            Value = "firefox",
            Field = TriggerField.WindowClass,
            MatchMode = TriggerMatchMode.Contains,
            Action = TriggerOperation.RunMacro,
            MacroFilePath = macroPath,
            FireMode = TriggerFireMode.EveryMatch,
            IsEnabled = true,
        };
        _service.AddTask(task);

        _ = _windowManager.GetActiveWindowAsync(Arg.Any<CancellationToken>())
            .Returns(new WindowInfo { Class = "firefox", Title = "Firefox" });

        await _service.PollOnceAsync(CancellationToken.None);

        _ = await _macroFileManager.Received(1).LoadAsync(macroPath);
        await _macroPlayer.Received(1).PlayAsync(macro, options: null, Arg.Any<CancellationToken>());
        _ = task.LastStatus.Should().Contain("Ran macro");
    }

    [Fact]
    public async Task PollOnce_RunMacroAction_MissingFile_DoesNotPlay()
    {
        var task = new TriggerTask
        {
            Value = "firefox",
            Field = TriggerField.WindowClass,
            MatchMode = TriggerMatchMode.Contains,
            Action = TriggerOperation.RunMacro,
            MacroFilePath = "/nonexistent/demo.macro",
            FireMode = TriggerFireMode.EveryMatch,
            IsEnabled = true,
        };
        _service.AddTask(task);

        _ = _windowManager.GetActiveWindowAsync(Arg.Any<CancellationToken>())
            .Returns(new WindowInfo { Class = "firefox", Title = "Firefox" });

        await _service.PollOnceAsync(CancellationToken.None);

        _ = await _macroFileManager.DidNotReceive().LoadAsync(Arg.Any<string>());
        await _macroPlayer.DidNotReceive().PlayAsync(
            Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions?>(), Arg.Any<CancellationToken>());
        _ = task.LastStatus.Should().Be("Macro file not found");
    }

    [Fact]
    public async Task PollOnce_WorkspaceCondition_SkipsWindowQuery()
    {
        // Workspace-only task should not require GetActiveWindowAsync to return meaningful data.
        var task = new TriggerTask
        {
            Value = "dev",
            Field = TriggerField.Workspace,
            MatchMode = TriggerMatchMode.Equals,
            Action = TriggerOperation.SwitchProfile,
            TargetProfileId = "work",
            FireMode = TriggerFireMode.EveryMatch,
            IsEnabled = true,
        };
        _service.AddTask(task);

        _ = _windowManager.GetActiveWindowAsync(Arg.Any<CancellationToken>())
            .Returns((WindowInfo?)null);
        _ = _windowManager.GetActiveWorkspaceAsync(Arg.Any<CancellationToken>())
            .Returns("dev");

        await _service.PollOnceAsync(CancellationToken.None);

        await _profileSwitchRequests.Received(1).RequestSwitchAsync("work");
    }

    [Fact]
    public async Task PollOnce_OnExit_Fires_WhenMatchBreaks()
    {
        var task = new TriggerTask
        {
            Value = "firefox",
            Field = TriggerField.WindowClass,
            MatchMode = TriggerMatchMode.Contains,
            Action = TriggerOperation.SwitchProfile,
            TargetProfileId = "work",
            FireMode = TriggerFireMode.OnExit,
            IsEnabled = true,
        };
        _service.AddTask(task);

        // Poll 1: matches → no fire (OnExit only fires on break).
        _ = _windowManager.GetActiveWindowAsync(Arg.Any<CancellationToken>())
            .Returns(new WindowInfo { Class = "firefox", Title = "Firefox" });
        await _service.PollOnceAsync(CancellationToken.None);
        await _profileSwitchRequests.DidNotReceive().RequestSwitchAsync(Arg.Any<string>());

        // Poll 2: no longer matching → fire
        _ = _windowManager.GetActiveWindowAsync(Arg.Any<CancellationToken>())
            .Returns(new WindowInfo { Class = "terminal", Title = "Terminal" });
        await _service.PollOnceAsync(CancellationToken.None);

        await _profileSwitchRequests.Received(1).RequestSwitchAsync("work");
    }

    [Fact]
    public async Task PollOnce_OnExit_DoesNotFire_WhenMatchNeverStarted()
    {
        var task = new TriggerTask
        {
            Value = "firefox",
            Field = TriggerField.WindowClass,
            MatchMode = TriggerMatchMode.Contains,
            Action = TriggerOperation.SwitchProfile,
            TargetProfileId = "work",
            FireMode = TriggerFireMode.OnExit,
            IsEnabled = true,
        };
        _service.AddTask(task);

        _ = _windowManager.GetActiveWindowAsync(Arg.Any<CancellationToken>())
            .Returns(new WindowInfo { Class = "terminal", Title = "Terminal" });
        await _service.PollOnceAsync(CancellationToken.None);

        await _profileSwitchRequests.DidNotReceive().RequestSwitchAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task PollOnce_CooldownMs_SuppressesRapidRefire()
    {
        var task = new TriggerTask
        {
            Value = "firefox",
            Field = TriggerField.WindowClass,
            MatchMode = TriggerMatchMode.Contains,
            Action = TriggerOperation.SwitchProfile,
            TargetProfileId = "work",
            FireMode = TriggerFireMode.EveryMatch,
            CooldownMs = 10000, // 10s — far beyond the test window
            IsEnabled = true,
        };
        _service.AddTask(task);

        _ = _windowManager.GetActiveWindowAsync(Arg.Any<CancellationToken>())
            .Returns(new WindowInfo { Class = "firefox", Title = "Firefox" });

        await _service.PollOnceAsync(CancellationToken.None);
        await _service.PollOnceAsync(CancellationToken.None);

        // Cooldown blocks the second fire.
        await _profileSwitchRequests.Received(1).RequestSwitchAsync("work");
    }

    [Fact]
    public async Task PollOnce_CooldownMs_AllowsRefire_AfterWindow()
    {
        var task = new TriggerTask
        {
            Value = "firefox",
            Field = TriggerField.WindowClass,
            MatchMode = TriggerMatchMode.Contains,
            Action = TriggerOperation.SwitchProfile,
            TargetProfileId = "work",
            FireMode = TriggerFireMode.EveryMatch,
            CooldownMs = 0, // disabled — every match fires
            IsEnabled = true,
        };
        _service.AddTask(task);

        _ = _windowManager.GetActiveWindowAsync(Arg.Any<CancellationToken>())
            .Returns(new WindowInfo { Class = "firefox", Title = "Firefox" });
        await _service.PollOnceAsync(CancellationToken.None);
        await _service.PollOnceAsync(CancellationToken.None);

        await _profileSwitchRequests.Received(2).RequestSwitchAsync("work");
    }

    [Fact]
    public async Task PollOnce_IntervalNone_FiresEveryTick_RegardlessOfWindow()
    {
        var task = new TriggerTask
        {
            Field = TriggerField.None,
            Action = TriggerOperation.SwitchProfile,
            TargetProfileId = "work",
            FireMode = TriggerFireMode.EveryMatch,
            IsEnabled = true,
        };
        _service.AddTask(task);

        _ = _windowManager.GetActiveWindowAsync(Arg.Any<CancellationToken>())
            .Returns((WindowInfo?)null);

        await _service.PollOnceAsync(CancellationToken.None);
        await _service.PollOnceAsync(CancellationToken.None);

        await _profileSwitchRequests.Received(2).RequestSwitchAsync("work");
    }

    [Fact]
    public async Task PollOnce_RegexMatch_FiresOnPattern()
    {
        var task = new TriggerTask
        {
            Value = "^Fire(?:fox|bird)$",
            Field = TriggerField.WindowClass,
            MatchMode = TriggerMatchMode.Regex,
            Action = TriggerOperation.SwitchProfile,
            TargetProfileId = "work",
            FireMode = TriggerFireMode.EveryMatch,
            IsEnabled = true,
        };
        _service.AddTask(task);

        _ = _windowManager.GetActiveWindowAsync(Arg.Any<CancellationToken>())
            .Returns(new WindowInfo { Class = "Firefox", Title = "Firefox" });

        await _service.PollOnceAsync(CancellationToken.None);

        await _profileSwitchRequests.Received(1).RequestSwitchAsync("work");
    }

    [Fact]
    public async Task PollOnce_RegexMatch_DoesNotFireOnMismatch()
    {
        var task = new TriggerTask
        {
            Value = "^Firefox$",
            Field = TriggerField.WindowClass,
            MatchMode = TriggerMatchMode.Regex,
            Action = TriggerOperation.SwitchProfile,
            TargetProfileId = "work",
            FireMode = TriggerFireMode.EveryMatch,
            IsEnabled = true,
        };
        _service.AddTask(task);

        _ = _windowManager.GetActiveWindowAsync(Arg.Any<CancellationToken>())
            .Returns(new WindowInfo { Class = "chrome", Title = "Chrome" });

        await _service.PollOnceAsync(CancellationToken.None);

        await _profileSwitchRequests.DidNotReceive().RequestSwitchAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task PollOnce_RegexMatch_InvalidPattern_DoesNotThrow_TreatsAsNonMatch()
    {
        var task = new TriggerTask
        {
            Value = "[invalid", // unbalanced bracket — invalid regex
            Field = TriggerField.WindowClass,
            MatchMode = TriggerMatchMode.Regex,
            Action = TriggerOperation.SwitchProfile,
            TargetProfileId = "work",
            FireMode = TriggerFireMode.EveryMatch,
            IsEnabled = true,
        };
        _service.AddTask(task);

        _ = _windowManager.GetActiveWindowAsync(Arg.Any<CancellationToken>())
            .Returns(new WindowInfo { Class = "anything", Title = "" });

        // Should not throw, should not fire.
        _ = await _service.Invoking(s => s.PollOnceAsync(CancellationToken.None))
            .Should().NotThrowAsync();
        await _profileSwitchRequests.DidNotReceive().RequestSwitchAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task PollOnce_DebounceMs_DelaysFire_UntilStable()
    {
        var task = new TriggerTask
        {
            Value = "firefox",
            Field = TriggerField.WindowClass,
            MatchMode = TriggerMatchMode.Contains,
            Action = TriggerOperation.SwitchProfile,
            TargetProfileId = "work",
            FireMode = TriggerFireMode.OnceOnChange,
            // A small debounce of 1ms to keep the test execution fast.
            // First matched poll records start timestamp and suppresses; the next poll
            // (running after the 1ms window) clears the debounce and fires.
            DebounceMs = 1,
            IsEnabled = true,
        };
        _service.AddTask(task);

        _ = _windowManager.GetActiveWindowAsync(Arg.Any<CancellationToken>())
            .Returns(new WindowInfo { Class = "firefox", Title = "Firefox" });

        // First poll: debounce records start, no fire.
        await _service.PollOnceAsync(CancellationToken.None);
        await _profileSwitchRequests.DidNotReceive().RequestSwitchAsync(Arg.Any<string>());

        // Wait past the 1ms debounce window, then poll again — match stable, should fire.
        await Task.Delay(20);
        await _service.PollOnceAsync(CancellationToken.None);

        await _profileSwitchRequests.Received(1).RequestSwitchAsync("work");
    }

    [Fact]
    public async Task PollOnce_DebounceMs_ResetOnMatchBreak()
    {
        var task = new TriggerTask
        {
            Value = "firefox",
            Field = TriggerField.WindowClass,
            MatchMode = TriggerMatchMode.Contains,
            Action = TriggerOperation.SwitchProfile,
            TargetProfileId = "work",
            FireMode = TriggerFireMode.OnceOnChange,
            DebounceMs = 1000,
            IsEnabled = true,
        };
        _service.AddTask(task);

        // Match → break → match within the debounce window: should not fire.
        _ = _windowManager.GetActiveWindowAsync(Arg.Any<CancellationToken>())
            .Returns(new WindowInfo { Class = "firefox", Title = "Firefox" });
        await _service.PollOnceAsync(CancellationToken.None);

        _ = _windowManager.GetActiveWindowAsync(Arg.Any<CancellationToken>())
            .Returns(new WindowInfo { Class = "terminal", Title = "Terminal" });
        await _service.PollOnceAsync(CancellationToken.None);

        _ = _windowManager.GetActiveWindowAsync(Arg.Any<CancellationToken>())
            .Returns(new WindowInfo { Class = "firefox", Title = "Firefox" });
        await _service.PollOnceAsync(CancellationToken.None);

        await _profileSwitchRequests.DidNotReceive().RequestSwitchAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task SaveAndLoadAsync_RoundTripsTasks()
    {
        var task = new TriggerTask
        {
            Name = "Test Trigger",
            Value = "firefox",
            Field = TriggerField.WindowClass,
            MatchMode = TriggerMatchMode.Contains,
            Action = TriggerOperation.SwitchProfile,
            TargetProfileId = "gaming",
            FireMode = TriggerFireMode.OnceOnChange,
            IsEnabled = false,
        };
        _service.AddTask(task);
        await _service.SaveAsync();

        // New service instance loads from same file
        using var service2 = new TriggerService(
            _windowManager, _profileSwitchRequests, _macroFileManager, () => _macroPlayer, _triggersFilePath);
        await service2.LoadAsync();

        _ = service2.Tasks.Should().HaveCount(1);
        var loaded = service2.Tasks[0];
        _ = loaded.Name.Should().Be("Test Trigger");
        _ = loaded.Value.Should().Be("firefox");
        _ = loaded.Field.Should().Be(TriggerField.WindowClass);
        _ = loaded.Action.Should().Be(TriggerOperation.SwitchProfile);
        _ = loaded.TargetProfileId.Should().Be("gaming");
        _ = loaded.FireMode.Should().Be(TriggerFireMode.OnceOnChange);
        _ = loaded.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_WithCapturedSynchronizationContext_CompletesBeforeDeferredCollectionUpdate()
    {
        var task = new TriggerTask { Name = "Captured Context Trigger", Value = "firefox" };
        _service.AddTask(task);
        await _service.SaveAsync();

        var context = new DeferredSynchronizationContext();
        var previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            using var service2 = new TriggerService(
                _windowManager, _profileSwitchRequests, _macroFileManager, () => _macroPlayer, _triggersFilePath);
            SynchronizationContext.SetSynchronizationContext(syncContext: null);
            try
            {
                var loadTask = Task.Run(service2.LoadAsync);
                await context.PostObserved.Task.WaitAsync(TimeSpan.FromSeconds(2));

                _ = loadTask.IsCompleted.Should().BeFalse();
                _ = service2.Tasks.Should().BeEmpty();
                _ = context.PendingCallbacks.Should().Be(1);

                context.Drain();
                await loadTask.WaitAsync(TimeSpan.FromSeconds(2));
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(context);
            }

            _ = service2.Tasks.Should().ContainSingle().Which.Name.Should().Be("Captured Context Trigger");
            _ = context.PendingCallbacks.Should().Be(0);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
    }

    [Fact]
    public async Task ReloadAsync_LoadsFromNewProfileDirectory()
    {
        var task = new TriggerTask
        {
            Name = "Profile Trigger",
            Value = "terminal",
            Field = TriggerField.WindowClass,
            Action = TriggerOperation.SwitchProfile,
            TargetProfileId = "work",
            IsEnabled = false,
        };
        _service.AddTask(task);
        await _service.SaveAsync();

        // Create a second profile directory with a different triggers file
        var profileDir = Path.Combine(_testRootDirectory, "profile2");
        _ = Directory.CreateDirectory(profileDir);
        var profileTriggersPath = Path.Combine(profileDir, "triggers.json");
        File.Copy(_triggersFilePath, profileTriggersPath);

        using var service2 = new TriggerService(
            _windowManager, _profileSwitchRequests, _macroFileManager, () => _macroPlayer, _triggersFilePath);
        await service2.ReloadAsync(profileDir);

        _ = service2.Tasks.Should().HaveCount(1);
        _ = service2.Tasks[0].Name.Should().Be("Profile Trigger");
    }

    private sealed class DeferredSynchronizationContext : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback Callback, object? State)> _callbacks = new();

        public TaskCompletionSource PostObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int PendingCallbacks
        {
            get
            {
                lock (_callbacks)
                {
                    return _callbacks.Count;
                }
            }
        }

        public override void Post(SendOrPostCallback callback, object? state)
        {
            lock (_callbacks)
            {
                _callbacks.Enqueue((callback, state));
            }

            _ = PostObserved.TrySetResult();
        }

        public void Drain()
        {
            while (true)
            {
                (SendOrPostCallback Callback, object? State) callback;
                lock (_callbacks)
                {
                    if (_callbacks.Count is 0)
                    {
                        return;
                    }

                    callback = _callbacks.Dequeue();
                }

                callback.Callback(callback.State);
            }
        }
    }
}
