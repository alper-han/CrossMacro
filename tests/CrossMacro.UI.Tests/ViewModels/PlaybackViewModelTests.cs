
namespace CrossMacro.UI.Tests.ViewModels;

public sealed class PlaybackViewModelTests : IDisposable
{
    private readonly IMacroPlayer _player;
    private readonly ISettingsService _settingsService;
    private readonly AppSettings _settings;
    private readonly LoadedMacroSession _loadedMacroSession;
    private readonly ILocalizationService _localizationService;
    private readonly IDialogService _dialogService;
    private readonly PlaybackViewModel _viewModel;

    public PlaybackViewModelTests()
    {
        _player = Substitute.For<IMacroPlayer>();
        _settingsService = Substitute.For<ISettingsService>();
        _localizationService = Substitute.For<ILocalizationService>();
        _dialogService = Substitute.For<IDialogService>();
        _ = _localizationService.CurrentCulture.Returns(System.Globalization.CultureInfo.GetCultureInfo("en"));
        _ = _localizationService[Arg.Any<string>()].Returns(call => call.Arg<string>() switch
        {
            "Playback_StatusReady" => "[Playback_StatusReady]",
            "Playback_StatusPlaying" => "[Playback_StatusPlaying]",
            "Playback_StatusComplete" => "[Playback_StatusComplete]",
            "Playback_StatusStopped" => "[Playback_StatusStopped]",
            "Playback_StatusPaused" => "[Playback_StatusPaused]",
            "Playback_StatusError" => "[Playback_StatusError] {0}",
            "Playback_StatusWaitingNextSequence" => "[Playback_StatusWaitingNextSequence] {0}",
            "Playback_StatusSequencePlaying" => "[Playback_StatusSequencePlaying] {0} | {1} | {2} | {3} | {4}",
            "Playback_UnnamedMacro" => "[Playback_UnnamedMacro]",
            "Playback_SequenceCycleInfinite" => "[Playback_SequenceCycleInfinite] {0}",
            "Playback_SequenceRepeatProgress" => "[Playback_SequenceRepeatProgress] {0} | {1}",
            "Playback_StatusWaitingNextLoop" => "[Playback_StatusWaitingNextLoop] {0}",
            "Playback_StatusLoopInfinite" => "[Playback_StatusLoopInfinite] {0}",
            "Playback_StatusLoopProgress" => "[Playback_StatusLoopProgress] {0} | {1}",
            "Playback_StatusStartingIn" => "[Playback_StatusStartingIn] {0}",
            "Playback_AbsoluteCoordinatesUnsupportedTitle" => "[Playback_AbsoluteCoordinatesUnsupportedTitle]",
            "Playback_AbsoluteCoordinatesUnsupportedMessage" => "[Playback_AbsoluteCoordinatesUnsupportedMessage]",
            "Playback_StatusAbsoluteCoordinatesUnsupported" => "[Playback_StatusAbsoluteCoordinatesUnsupported]",
            "Playback_PermissionRequiredTitle" => "[Playback_PermissionRequiredTitle]",
            "Playback_PermissionRequiredMessage" => "[Playback_PermissionRequiredMessage]",
            "Playback_StatusPermissionRequired" => "[Playback_StatusPermissionRequired]",
            "Playback_FastLoopWarningTitle" => "[Playback_FastLoopWarningTitle]",
            "Playback_FastLoopWarningMessage" => "[Playback_FastLoopWarningMessage]",
            "Playback_FastLoopWarningContinue" => "[Playback_FastLoopWarningContinue]",
            "Playback_FastLoopWarningPlay" => "[Playback_FastLoopWarningPlay]",
            "Playback_FastLoopWarningCancel" => "[Playback_FastLoopWarningCancel]",
            "Playback_FastLoopWarningAbort" => "[Playback_FastLoopWarningAbort]",
            "Playback_FastLoopWarningSuppress" => "[Playback_FastLoopWarningSuppress]",
            _ => call.Arg<string>(),
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
            CountdownSeconds = 0,
        };
        _loadedMacroSession = new LoadedMacroSession(_localizationService);

        _ = _settingsService.Current.Returns(_settings);
        _ = _settingsService.SaveAfterIdleAsync().Returns(Task.CompletedTask);
        _ = _dialogService.ShowFastLoopWarningAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>())
            .Returns(Task.FromResult(new FastLoopWarningResult(ContinuePlayback: true, SuppressFutureWarnings: false)));
        _ = _player.CurrentLoop.Returns(1);
        _ = _player.TotalLoops.Returns(1);
        _ = _player.IsWaitingBetweenLoops.Returns(returnThis: false);
        _ = _player.PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _viewModel = new PlaybackViewModel(_player, _settingsService, _loadedMacroSession, _localizationService, _dialogService);
    }

    public void Dispose()
    {
        _viewModel.Dispose();
    }

    [Fact]
    public void Constructor_InitializesPropertiesFromSettings()
    {
        _ = _viewModel.PlaybackSpeed.Should().Be(1.0);
        _ = _viewModel.MotionPlaybackMode.Should().Be(MotionPlaybackMode.Precision);
        _ = _viewModel.IsPrecisionMotionMode.Should().BeTrue();
        _ = _viewModel.ShowPrecisionMotionRate.Should().BeTrue();
        _ = _viewModel.PrecisionMotionEventsPerSecond.Should().Be(PlaybackOptions.DefaultPrecisionMotionEventsPerSecond);
        _ = _viewModel.ShowStrictSpeedMotionRate.Should().BeFalse();
        _ = _viewModel.IsLooping.Should().BeFalse();
        _ = _viewModel.LoopCount.Should().Be(1);
        _ = _viewModel.LoopDelayMs.Should().Be(0);
        _ = _viewModel.UseRandomLoopDelay.Should().BeFalse();
        _ = _viewModel.LoopDelayMinMs.Should().Be(0);
        _ = _viewModel.LoopDelayMaxMs.Should().Be(0);
    }

    [Fact]
    public async Task MotionMode_StrictSpeed_PersistsAndFlowsIntoPlaybackOptions()
    {
        var macro = CreateMacro();
        _viewModel.MotionPlaybackMode = MotionPlaybackMode.StrictSpeed;
        _viewModel.StrictSpeedMotionEventsPerSecond = 240;
        _viewModel.SetMacro(macro);

        await _viewModel.PlayMacroAsync();

        _ = _viewModel.IsStrictSpeedMotionMode.Should().BeTrue();
        _ = _viewModel.ShowStrictSpeedMotionRate.Should().BeTrue();
        _ = _settings.MotionMode.Should().Be(MotionPlaybackMode.StrictSpeed);
        _ = _settings.StrictSpeedMotionEventsPerSecond.Should().Be(240);
        await _player.Received(1).PlayAsync(
            macro,
            Arg.Is<PlaybackOptions>(options =>
                options.MotionMode == MotionPlaybackMode.StrictSpeed
                && options.StrictSpeedMotionEventsPerSecond == 240),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MotionMode_Precision_PersistsQualityCeilingAndFlowsIntoPlaybackOptions()
    {
        var macro = CreateMacro();
        _viewModel.PrecisionMotionEventsPerSecond = 320;
        _viewModel.SetMacro(macro);

        await _viewModel.PlayMacroAsync();

        _ = _viewModel.ShowPrecisionMotionRate.Should().BeTrue();
        _ = _settings.PrecisionMotionEventsPerSecond.Should().Be(320);
        await _player.Received(1).PlayAsync(
            macro,
            Arg.Is<PlaybackOptions>(options =>
                options.MotionMode == MotionPlaybackMode.Precision
                && options.PrecisionMotionEventsPerSecond == 320),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void RandomLoopDelay_TogglesVisibleInputs()
    {
        _viewModel.IsLooping = true;

        _ = _viewModel.ShowFixedLoopDelayInput.Should().BeTrue();
        _ = _viewModel.ShowRandomLoopDelayInputs.Should().BeFalse();

        _viewModel.UseRandomLoopDelay = true;

        _ = _viewModel.ShowFixedLoopDelayInput.Should().BeFalse();
        _ = _viewModel.ShowRandomLoopDelayInputs.Should().BeTrue();
    }

    [Fact]
    public void RandomLoopDelay_MaxClampsToMin()
    {
        _viewModel.UseRandomLoopDelay = true;
        _viewModel.LoopDelayMinMs = 300;
        _viewModel.LoopDelayMaxMs = 100;

        _ = _viewModel.LoopDelayMinMs.Should().Be(300);
        _ = _viewModel.LoopDelayMaxMs.Should().Be(300);
    }

    [Fact]
    public void LoadedMacroSessionSelectionChange_RefreshesPlaybackAvailability()
    {
        _ = _viewModel.CanPlayMacro.Should().BeFalse();

        _ = _loadedMacroSession.AddMacro(CreateMacro());

        _ = _viewModel.HasMacro.Should().BeTrue();
        _ = _viewModel.CanPlayMacro.Should().BeTrue();
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
    public async Task PlayMacroAsync_WhenPlayerCompletesOffThread_RaisesCompletionOnUiExecutorAfterCleanup()
    {
        var macro = CreateMacro();
        var playStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var playbackCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanupStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowPlayerReturn = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = _player.PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
            .Returns(async unusedCallInfo =>
            {
                playStarted.SetResult(true);
                _ = await playbackCompleted.Task.ConfigureAwait(false);
                cleanupStarted.SetResult(true);
                _ = await allowPlayerReturn.Task.ConfigureAwait(false);
            });

        _viewModel.SetMacro(macro);
        _viewModel.CanPlayMacroExternal = true;

        var uiExecutor = new DeferredUiExecutor();
        var completionContexts = new List<SynchronizationContext?>();
        _viewModel.PlaybackStateChanged += (_, isPlaying) =>
        {
            if (!isPlaying)
            {
                completionContexts.Add(SynchronizationContext.Current);
            }
        };

        var previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(uiExecutor);
        Task playTask;
        try
        {
            playTask = _viewModel.PlayMacroAsync();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }

        _ = await playStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        _ = await Task.Run(() => playbackCompleted.TrySetResult(true));
        _ = await cleanupStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        _ = _viewModel.IsPlaying.Should().BeTrue();
        _ = completionContexts.Should().BeEmpty();

        _ = allowPlayerReturn.TrySetResult(true);
        var firstCompleted = await Task.WhenAny(playTask, uiExecutor.PostObserved.Task);

        _ = firstCompleted.Should().BeSameAs(uiExecutor.PostObserved.Task);
        _ = _viewModel.IsPlaying.Should().BeTrue();
        _ = completionContexts.Should().BeEmpty();

        uiExecutor.RunAll();
        await playTask;

        _ = completionContexts.Should().ContainSingle().Which.Should().BeSameAs(uiExecutor);
        _ = _viewModel.IsPlaying.Should().BeFalse();
    }

    [Fact]
    public async Task PlayMacroAsync_WhenMacroHasOnlyScreenReadingScriptSteps_StartsPlayback()
    {
        var macro = new MacroSequence
        {
            Name = "Screen Reading Macro",
            ScriptSteps =
            {
                "pixelcolor 10 20 color",
                "waitcolor 11 22 00FFAA 2500",
                "pixelsearch 0 0 3 3 123456 x y",
            },
        };
        _viewModel.SetMacro(macro);
        _viewModel.CanPlayMacroExternal = true;

        _ = _viewModel.HasMacro.Should().BeTrue();
        _ = _viewModel.CanPlayMacro.Should().BeTrue();

        await _viewModel.PlayMacroAsync();

        await _player.Received(1).PlayAsync(macro, Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PlayMacroAsync_WhenRandomLoopDelayEnabled_ForwardsRandomDelayOptions()
    {
        var macro = CreateMacro();
        PlaybackOptions? capturedOptions = null;
        _ = _player.PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
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

        _ = capturedOptions.Should().NotBeNull();
        _ = capturedOptions!.Loop.Should().BeTrue();
        _ = capturedOptions.RepeatCount.Should().Be(3);
        _ = capturedOptions.RepeatDelayMs.Should().Be(90);
        _ = capturedOptions.UseRandomRepeatDelay.Should().BeTrue();
        _ = capturedOptions.RepeatDelayMinMs.Should().Be(120);
        _ = capturedOptions.RepeatDelayMaxMs.Should().Be(240);
    }

    [Fact]
    public async Task LoopDelayMs_WhenRiskyLoopSettingIsCancelled_RevertsTheDelay()
    {
        _settings.IsLooping = true;
        _settings.LoopCount = 2;
        _settings.LoopDelayMs = 100;
        _viewModel.RefreshProfileSettings();
        _dialogService.ClearReceivedCalls();
        _ = _dialogService.ShowFastLoopWarningAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>())
            .Returns(Task.FromResult(FastLoopWarningResult.Cancelled));

        _viewModel.LoopDelayMs = 99;

        await _dialogService.Received(1).ShowFastLoopWarningAsync(
            "[Playback_FastLoopWarningTitle]",
            "[Playback_FastLoopWarningMessage]",
            "[Playback_FastLoopWarningContinue]",
            "[Playback_FastLoopWarningCancel]",
            "[Playback_FastLoopWarningSuppress]");
        _ = _viewModel.LoopDelayMs.Should().Be(100);
        _ = _settings.LoopDelayMs.Should().Be(100);
    }

    [Fact]
    public async Task LoopDelayMs_WhenRiskyLoopSettingIsAccepted_CanSuppressFutureWarnings()
    {
        _settings.IsLooping = true;
        _settings.LoopCount = 2;
        _settings.LoopDelayMs = 100;
        _viewModel.RefreshProfileSettings();
        _dialogService.ClearReceivedCalls();
        _ = _dialogService.ShowFastLoopWarningAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>())
            .Returns(Task.FromResult(new FastLoopWarningResult(ContinuePlayback: true, SuppressFutureWarnings: true)));

        _viewModel.LoopDelayMs = 99;

        _ = _viewModel.LoopDelayMs.Should().Be(99);
        _ = _settings.SuppressFastLoopWarning.Should().BeTrue();
        await _dialogService.Received(1).ShowFastLoopWarningAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>());
    }

    [Fact]
    public async Task LoopCount_WhenItCreatesAFastLoopAndIsCancelled_RevertsTheRepeatCount()
    {
        _settings.IsLooping = true;
        _settings.LoopCount = 1;
        _settings.LoopDelayMs = 0;
        _viewModel.RefreshProfileSettings();
        _dialogService.ClearReceivedCalls();
        _ = _dialogService.ShowFastLoopWarningAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>())
            .Returns(Task.FromResult(FastLoopWarningResult.Cancelled));

        _viewModel.LoopCount = 9999;

        _ = _viewModel.LoopCount.Should().Be(1);
        _ = _settings.LoopCount.Should().Be(1);
        await _dialogService.Received(1).ShowFastLoopWarningAsync(
            "[Playback_FastLoopWarningTitle]",
            "[Playback_FastLoopWarningMessage]",
            "[Playback_FastLoopWarningContinue]",
            "[Playback_FastLoopWarningCancel]",
            "[Playback_FastLoopWarningSuppress]");
    }

    [Fact]
    public async Task PlayMacroAsync_WhenRiskySavedLoopSettingIsCancelled_DoesNotStartPlayback()
    {
        _settings.IsLooping = true;
        _settings.LoopCount = 9999;
        _settings.LoopDelayMs = 0;
        _viewModel.RefreshProfileSettings();
        _viewModel.SetMacro(CreateMacro());
        _ = _dialogService.ShowFastLoopWarningAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>())
            .Returns(Task.FromResult(FastLoopWarningResult.Cancelled));

        await _viewModel.PlayMacroAsync();

        await _player.DidNotReceive().PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>());
        await _dialogService.Received(1).ShowFastLoopWarningAsync(
            "[Playback_FastLoopWarningTitle]",
            "[Playback_FastLoopWarningMessage]",
            "[Playback_FastLoopWarningPlay]",
            "[Playback_FastLoopWarningAbort]",
            "[Playback_FastLoopWarningSuppress]");
    }

    [Fact]
    public async Task PlayMacroAsync_WhenRiskyRandomLoopCanSelectFastDelay_ShowsWarning()
    {
        _settings.IsLooping = true;
        _settings.LoopCount = 2;
        _settings.UseRandomLoopDelay = true;
        _settings.LoopDelayMinMs = 99;
        _settings.LoopDelayMaxMs = 200;
        _viewModel.RefreshProfileSettings();
        _viewModel.SetMacro(CreateMacro());
        _ = _dialogService.ShowFastLoopWarningAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>())
            .Returns(Task.FromResult(FastLoopWarningResult.Cancelled));

        await _viewModel.PlayMacroAsync();

        await _player.DidNotReceive().PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>());
        await _dialogService.Received(1).ShowFastLoopWarningAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            "[Playback_FastLoopWarningPlay]",
            "[Playback_FastLoopWarningAbort]",
            Arg.Any<string>());
    }

    [Fact]
    public async Task PlayMacroAsync_WhenLoopDelayIsAtLeastOneHundredMs_DoesNotShowWarning()
    {
        _settings.IsLooping = true;
        _settings.LoopCount = 9999;
        _settings.LoopDelayMs = 100;
        _viewModel.RefreshProfileSettings();
        var macro = CreateMacro();
        _viewModel.SetMacro(macro);

        await _viewModel.PlayMacroAsync();

        await _player.Received(1).PlayAsync(macro, Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>());
        await _dialogService.DidNotReceive().ShowFastLoopWarningAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>());
    }

    [Fact]
    public async Task PlayMacroAsync_WhenFastLoopWarningIsSuppressed_StartsWithoutShowingIt()
    {
        _settings.IsLooping = true;
        _settings.LoopCount = 9999;
        _settings.LoopDelayMs = 0;
        _settings.SuppressFastLoopWarning = true;
        _viewModel.RefreshProfileSettings();
        var macro = CreateMacro();
        _viewModel.SetMacro(macro);

        await _viewModel.PlayMacroAsync();

        await _player.Received(1).PlayAsync(macro, Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>());
        await _dialogService.DidNotReceive().ShowFastLoopWarningAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>());
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
        _ = _loadedMacroSession.SelectedMacroItem.Should().BeSameAs(second);
    }

    [Fact]
    public async Task PlayMacroAsync_WhenSequentialCycleModeAndSelectionIsNull_StartsFromFirstLoadedMacro()
    {
        var first = _loadedMacroSession.AddMacro(CreateMacro("First"));
        _ = _loadedMacroSession.AddMacro(CreateMacro("Second"));
        _loadedMacroSession.SelectedMacroItem = null;
        _loadedMacroSession.PlaybackMode = LoadedMacroPlaybackMode.SequentialCycle;

        var playedMacros = new List<MacroSequence>();
        _ = _player.PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                playedMacros.Add(callInfo.ArgAt<MacroSequence>(0));
                return Task.CompletedTask;
            });

        _ = _viewModel.HasMacro.Should().BeTrue();
        _ = _viewModel.CanPlayMacro.Should().BeTrue();

        await _viewModel.PlayMacroAsync();

        _ = playedMacros.Select(macro => macro.Name).Should().ContainInOrder("First", "Second");
        _ = _loadedMacroSession.SelectedMacroItem.Should().BeSameAs(first);
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
        _ = _player.PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                playedMacros.Add(callInfo.ArgAt<MacroSequence>(0));
                playedOptions.Add(callInfo.ArgAt<PlaybackOptions>(1));
                return Task.CompletedTask;
            });

        await _viewModel.PlayMacroAsync();

        _ = playedMacros.Select(macro => macro.Name).Should().ContainInOrder("Second", "Third", "First");
        _ = playedMacros.Select(macro => macro.Id).Should().ContainInOrder(second.Macro.Id, third.Macro.Id, first.Macro.Id);
        _ = playedOptions.Select(options => options.RepeatCount).Should().ContainInOrder(5, 2, 1);
        _ = playedOptions.Select(options => options.Loop).Should().ContainInOrder(true, true, false);
        _ = _loadedMacroSession.SelectedMacroItem.Should().BeSameAs(second);
    }

    [Fact]
    public async Task PlayMacroAsync_WhenSequentialCycleUsesRandomDelay_ResolvesInclusiveRangeThroughPlaybackPath()
    {
        var requestedRange = (min: 0, max: 0);
        var viewModel = new PlaybackViewModel(
            _player,
            _settingsService,
            _loadedMacroSession,
            _localizationService,
            _dialogService,
            (min, max) =>
            {
                requestedRange = (min, max);
                return max;
            });
        var first = _loadedMacroSession.AddMacro(CreateMacro("First"));
        _ = _loadedMacroSession.AddMacro(CreateMacro("Second"));
        _loadedMacroSession.SelectedMacroItem = first;
        _loadedMacroSession.PlaybackMode = LoadedMacroPlaybackMode.SequentialCycle;
        viewModel.IsLooping = true;
        viewModel.LoopCount = 2;
        viewModel.UseRandomLoopDelay = true;
        viewModel.LoopDelayMinMs = 4;
        viewModel.LoopDelayMaxMs = 9;

        await viewModel.PlayMacroAsync();

        _ = requestedRange.Should().Be((4, 9));
        await _player.Received(4).PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PlayMacroAsync_WhenSequentialCycleContainsInvalidLaterMacro_DoesNotStartPlayback()
    {
        var first = _loadedMacroSession.AddMacro(CreateMacro("First"));
        _ = _loadedMacroSession.AddMacro(new MacroSequence { Name = "Broken" });
        _loadedMacroSession.SelectedMacroItem = first;
        _loadedMacroSession.PlaybackMode = LoadedMacroPlaybackMode.SequentialCycle;

        await _viewModel.PlayMacroAsync();

        await _player.DidNotReceive().PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>());
        _ = _viewModel.IsPlaying.Should().BeFalse();
        _ = _viewModel.PlaybackStatus.Should().Contain("Broken");
        _ = _viewModel.PlaybackStatus.Should().Contain("has no events");
    }

    [Fact]
    public async Task PlayMacroAsync_WhenSequentialCycleContainsScreenReadingScriptOnlyMacro_AllowsPlayback()
    {
        var first = _loadedMacroSession.AddMacro(CreateMacro("First"));
        var scriptOnly = _loadedMacroSession.AddMacro(new MacroSequence
        {
            Name = "Screen Reading Macro",
            ScriptSteps = { "waitcolor 11 22 00FFAA 2500" },
        });
        _loadedMacroSession.SelectedMacroItem = first;
        _loadedMacroSession.PlaybackMode = LoadedMacroPlaybackMode.SequentialCycle;

        var playedMacros = new List<MacroSequence>();
        _ = _player.PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                playedMacros.Add(callInfo.ArgAt<MacroSequence>(0));
                return Task.CompletedTask;
            });

        await _viewModel.PlayMacroAsync();

        _ = playedMacros.Select(macro => macro.Name).Should().ContainInOrder("First", "Screen Reading Macro");
        _ = _loadedMacroSession.SelectedMacroItem.Should().BeSameAs(first);
        _ = scriptOnly.EventCount.Should().Be(1);
    }

    [Fact]
    public void CultureChanged_WhenIdle_RefreshesReadyStatusImmediately()
    {
        _ = _localizationService["Playback_StatusReady"].Returns("[Playback_StatusReady:updated]");

        _localizationService.CultureChanged += Raise.Event<EventHandler>(_localizationService, EventArgs.Empty);

        _ = _viewModel.PlaybackStatus.Should().Be("[Playback_StatusReady:updated]");
    }

    [Fact]
    public async Task CultureChanged_WhenSequencePlaying_UsesLocalizedSequenceFragments()
    {
        await using var harness = new BlockingPlaybackHarness(blockOnPlaybackInvocation: 5);
        _ = harness.Player.CurrentLoop.Returns(2);
        _ = harness.Player.TotalLoops.Returns(4);
        _ = harness.LocalizationService["Files_UnnamedMacro"].Returns(string.Empty);
        _ = harness.LocalizationService["Playback_UnnamedMacro"].Returns("[Playback_UnnamedMacro:updated]");
        _ = harness.LocalizationService["Playback_SequenceCycleInfinite"].Returns("[Playback_SequenceCycleInfinite:updated] {0}");
        _ = harness.LocalizationService["Playback_SequenceRepeatProgress"].Returns("[Playback_SequenceRepeatProgress:updated] {0} | {1}");

        var unnamedItem = harness.LoadedMacroSession.AddMacro(CreateMacro(string.Empty));
        unnamedItem.SequenceRepeatCount = 4;
        _ = harness.LoadedMacroSession.AddMacro(CreateMacro("Second"));
        harness.LoadedMacroSession.SelectedMacroItem = unnamedItem;
        harness.LoadedMacroSession.PlaybackMode = LoadedMacroPlaybackMode.SequentialCycle;
        harness.ViewModel.SetMacro(unnamedItem.Macro);
        harness.ViewModel.CanPlayMacroExternal = true;
        harness.ViewModel.IsLooping = true;
        harness.ViewModel.LoopCount = 0;

        await harness.StartPlaybackAsync();

        harness.LocalizationService.CultureChanged += Raise.Event<EventHandler>(harness.LocalizationService, EventArgs.Empty);

        _ = harness.ViewModel.PlaybackStatus.Should().Be(
            "[Playback_StatusSequencePlaying] [Playback_UnnamedMacro:updated] | 1 | 2 | [Playback_SequenceRepeatProgress:updated] 2 | 4 | [Playback_SequenceCycleInfinite:updated] 3");
    }

    [Fact]
    public async Task PlayMacroAsync_WhenSequentialCycleModeHasSingleLoadedMacro_UsesSequenceRepeatCount()
    {
        var item = _loadedMacroSession.AddMacro(CreateMacro("Only"));
        item.SequenceRepeatCount = 4;
        _loadedMacroSession.PlaybackMode = LoadedMacroPlaybackMode.SequentialCycle;

        PlaybackOptions? capturedOptions = null;
        MacroSequence? capturedMacro = null;
        _ = _player.PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedMacro = callInfo.ArgAt<MacroSequence>(0);
                capturedOptions = callInfo.ArgAt<PlaybackOptions>(1);
                return Task.CompletedTask;
            });

        await _viewModel.PlayMacroAsync();

        _ = capturedMacro.Should().NotBeNull();
        _ = capturedMacro!.Should().NotBeSameAs(item.Macro);
        _ = capturedMacro.Id.Should().Be(item.Macro.Id);
        _ = capturedMacro.Name.Should().Be(item.Macro.Name);
        _ = capturedOptions.Should().NotBeNull();
        _ = capturedOptions!.Loop.Should().BeTrue();
        _ = capturedOptions.RepeatCount.Should().Be(4);
        _ = _loadedMacroSession.SelectedMacroItem.Should().BeSameAs(item);
    }

    [Fact]
    public async Task PlayMacroAsync_WhenSequentialCycleStopped_RestoresOriginalSelection()
    {
        _ = _loadedMacroSession.AddMacro(CreateMacro("First"));
        var second = _loadedMacroSession.AddMacro(CreateMacro("Second"));
        _ = _loadedMacroSession.AddMacro(CreateMacro("Third"));

        _loadedMacroSession.SelectedMacroItem = second;
        _loadedMacroSession.PlaybackMode = LoadedMacroPlaybackMode.SequentialCycle;

        var invocationCount = 0;
        _ = _player.PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                invocationCount++;
                if (invocationCount is 2)
                {
                    _viewModel.StopPlayback();
                }

                return Task.CompletedTask;
            });

        await _viewModel.PlayMacroAsync();

        _ = _loadedMacroSession.SelectedMacroItem.Should().BeSameAs(second);
        _ = _viewModel.PlaybackStatus.Should().Be("[Playback_StatusStopped]");
        _player.Received(1).StopPlayback();
    }

    [Fact]
    public async Task StopPlayback_WhenTeardownStillRunning_KeepsStoppedStatus()
    {
        await using var harness = new BlockingPlaybackHarness();
        harness.SetSingleMacro(CreateMacro());

        await harness.StartPlaybackAsync();

        harness.ViewModel.StopPlayback();

        harness.Player.Received(1).StopPlayback();
        _ = harness.ViewModel.PlaybackStatus.Should().Be("[Playback_StatusStopped]");
        _ = harness.ViewModel.IsPlaying.Should().BeTrue();
    }

    [Fact]
    public async Task TogglePause_WhenPlaying_PausesOrResumes()
    {
        await using var harness = new BlockingPlaybackHarness();
        harness.SetSingleMacro(CreateMacro());

        await harness.StartPlaybackAsync();

        _ = harness.Player.IsPaused.Returns(returnThis: false);

        harness.ViewModel.TogglePause();

        harness.Player.Received(1).Pause();
        _ = harness.ViewModel.IsPaused.Should().BeTrue();
        _ = harness.ViewModel.PlaybackStatus.Should().Be("[Playback_StatusPaused]");

        _ = harness.Player.IsPaused.Returns(returnThis: true);

        harness.ViewModel.TogglePause();

        harness.Player.Received(1).ResumePlayback();
        _ = harness.ViewModel.IsPaused.Should().BeFalse();
    }

    [Fact]
    public async Task StopPlayback_WhenPlaying_StopsPlayerAndSetsStatus()
    {
        await using var harness = new BlockingPlaybackHarness();
        harness.SetSingleMacro(CreateMacro());

        await harness.StartPlaybackAsync();

        harness.ViewModel.StopPlayback();

        harness.Player.Received(1).StopPlayback();
        _ = harness.ViewModel.PlaybackStatus.Should().Be("[Playback_StatusStopped]");
        _ = harness.ViewModel.IsPlaying.Should().BeTrue();

        await harness.ReleaseAndAwaitPlaybackAsync();

        _ = harness.ViewModel.IsPlaying.Should().BeFalse();
    }

    [Fact]
    public async Task PlayMacroAsync_WhenPlayerThrows_SetsErrorStatusAndResetsPlaying()
    {
        var macro = CreateMacro();
        _viewModel.SetMacro(macro);
        _viewModel.CanPlayMacroExternal = true;
        _ = _player.PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("simulator failed")));

        await _viewModel.PlayMacroAsync();

        _ = _viewModel.IsPlaying.Should().BeFalse();
        _ = _viewModel.PlaybackStatus.Should().Contain("[Playback_StatusError]");
        _ = _viewModel.PlaybackStatus.Should().Contain("simulator failed");
    }

    [Fact]
    public async Task PlayMacroAsync_WhenAbsoluteCoordinatePlaybackUnsupported_ShowsFriendlyDialogAndStatus()
    {
        var macro = CreateMacro();
        _viewModel.SetMacro(macro);
        _viewModel.CanPlayMacroExternal = true;
        _ = _player.PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new AbsolutePlaybackUnsupportedException("Tracking")));

        await _viewModel.PlayMacroAsync();

        _ = _viewModel.IsPlaying.Should().BeFalse();
        _ = _viewModel.PlaybackStatus.Should().Be("[Playback_StatusAbsoluteCoordinatesUnsupported]");
        _ = _viewModel.PlaybackStatus.Should().NotContain("Tracking");
        await _dialogService.Received(1).ShowMessageAsync(
            "[Playback_AbsoluteCoordinatesUnsupportedTitle]",
            "[Playback_AbsoluteCoordinatesUnsupportedMessage]",
            Arg.Any<string>());
    }

    [Fact]
    public async Task PlayMacroAsync_WhenPlaybackPermissionRequired_ShowsFriendlyDialogAndStatus()
    {
        var macro = CreateMacro();
        _viewModel.SetMacro(macro);
        _viewModel.CanPlayMacroExternal = true;
        _ = _player.PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InputInjectionPermissionRequiredException("permission missing")));

        await _viewModel.PlayMacroAsync();

        _ = _viewModel.IsPlaying.Should().BeFalse();
        _ = _viewModel.PlaybackStatus.Should().Be("[Playback_StatusPermissionRequired]");
        await _dialogService.Received(1).ShowMessageAsync(
            "[Playback_PermissionRequiredTitle]",
            "[Playback_PermissionRequiredMessage]",
            Arg.Any<string>());
    }

    [Fact]
    public void TogglePlayback_WhenCannotPlay_DoesNotInvokePlayer()
    {
        _viewModel.CanPlayMacroExternal = false;

        _viewModel.TogglePlayback();

        _player.DidNotReceive().StopPlayback();
        _ = _player.DidNotReceive().PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void PlaybackSpeed_WhenSaveFails_RollsBackValue()
    {
        _ = _settingsService.SaveAfterIdleAsync().Returns(Task.FromException(new InvalidOperationException("disk full")));

        _viewModel.PlaybackSpeed = 2.0;

        _ = _viewModel.PlaybackSpeed.Should().Be(1.0);
        _ = _settings.PlaybackSpeed.Should().Be(1.0);
    }

    [Fact]
    public void UseRandomLoopDelay_WhenSaveFails_RollsBackVisibilityState()
    {
        _viewModel.IsLooping = true;
        _settingsService.ClearReceivedCalls();
        _ = _settingsService.SaveAfterIdleAsync().Returns(Task.FromException(new InvalidOperationException("disk full")));

        _viewModel.UseRandomLoopDelay = true;

        _ = _viewModel.UseRandomLoopDelay.Should().BeFalse();
        _ = _settings.UseRandomLoopDelay.Should().BeFalse();
        _ = _viewModel.ShowFixedLoopDelayInput.Should().BeTrue();
        _ = _viewModel.ShowRandomLoopDelayInputs.Should().BeFalse();
    }

    private sealed class BlockingPlaybackHarness : IAsyncDisposable
    {
        private readonly TaskCompletionSource<bool> _playbackStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _allowPlaybackCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly int _blockOnPlaybackInvocation;
        private int _playbackInvocationCount;

        public BlockingPlaybackHarness(int blockOnPlaybackInvocation = 1)
        {
            _blockOnPlaybackInvocation = blockOnPlaybackInvocation;
            Player = Substitute.For<IMacroPlayer>();
            SettingsService = Substitute.For<ISettingsService>();
            LocalizationService = Substitute.For<ILocalizationService>();
            var dialogService = Substitute.For<IDialogService>();
            var settings = new AppSettings();

            _ = LocalizationService.CurrentCulture.Returns(System.Globalization.CultureInfo.GetCultureInfo("en"));
            _ = LocalizationService[Arg.Any<string>()].Returns(call => call.Arg<string>() switch
            {
                "Playback_StatusReady" => "[Playback_StatusReady]",
                "Playback_StatusPlaying" => "[Playback_StatusPlaying]",
                "Playback_StatusComplete" => "[Playback_StatusComplete]",
                "Playback_StatusStopped" => "[Playback_StatusStopped]",
                "Playback_StatusPaused" => "[Playback_StatusPaused]",
                "Playback_StatusSequencePlaying" => "[Playback_StatusSequencePlaying] {0} | {1} | {2} | {3} | {4}",
                "Playback_UnnamedMacro" => "[Playback_UnnamedMacro]",
                "Playback_SequenceCycleInfinite" => "[Playback_SequenceCycleInfinite] {0}",
                "Playback_SequenceRepeatProgress" => "[Playback_SequenceRepeatProgress] {0} | {1}",
                _ => call.Arg<string>(),
            });
            _ = SettingsService.Current.Returns(settings);
            _ = SettingsService.SaveAfterIdleAsync().Returns(Task.CompletedTask);
            _ = dialogService.ShowFastLoopWarningAsync(
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    Arg.Any<string>())
                .Returns(Task.FromResult(new FastLoopWarningResult(ContinuePlayback: true, SuppressFutureWarnings: false)));
            _ = Player.CurrentLoop.Returns(1);
            _ = Player.TotalLoops.Returns(1);
            _ = Player.IsWaitingBetweenLoops.Returns(returnThis: false);
            _ = Player.PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
                .Returns(async unusedCallInfo =>
                {
                    if (Interlocked.Increment(ref _playbackInvocationCount) != _blockOnPlaybackInvocation)
                    {
                        return;
                    }

                    _ = _playbackStarted.TrySetResult(true);
                    _ = await _allowPlaybackCompletion.Task.ConfigureAwait(false);
                });

            LoadedMacroSession = new LoadedMacroSession(LocalizationService);
            ViewModel = new PlaybackViewModel(
                Player,
                SettingsService,
                LoadedMacroSession,
                LocalizationService,
                dialogService,
                randomInclusive: (minimum, maximum) => minimum,
                executeOnUiThread: operation => operation());
        }

        public IMacroPlayer Player { get; }

        public ISettingsService SettingsService { get; }

        public ILocalizationService LocalizationService { get; }

        public LoadedMacroSession LoadedMacroSession { get; }

        public PlaybackViewModel ViewModel { get; }

        public Task PlaybackTask { get; private set; } = Task.CompletedTask;

        public void SetSingleMacro(MacroSequence macro)
        {
            ViewModel.SetMacro(macro);
            ViewModel.CanPlayMacroExternal = true;
        }

        public async Task StartPlaybackAsync()
        {
            PlaybackTask = ViewModel.PlayMacroAsync();
            _ = await _playbackStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        }

        public async Task ReleaseAndAwaitPlaybackAsync()
        {
            _ = _allowPlaybackCompletion.TrySetResult(true);
            await PlaybackTask.WaitAsync(TimeSpan.FromSeconds(2));
        }

        public async ValueTask DisposeAsync()
        {
            ViewModel.StopPlayback();
            await ReleaseAndAwaitPlaybackAsync();
            ViewModel.Dispose();
        }
    }

    private static MacroSequence CreateMacro(string name = "Test Macro")
    {
        return new MacroSequence
        {
            Name = name,
            Events = { new MacroEvent() },
        };
    }
}
