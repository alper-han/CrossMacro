namespace CrossMacro.UI.Tests.ViewModels;

public sealed class MainWindowViewModelTests : IDisposable
{
    private readonly MainWindowHarness _harness = MainWindowHarness.Create();
    private readonly MainWindowViewModel _viewModel;

    public MainWindowViewModelTests()
    {
        _viewModel = _harness.ViewModel;
    }

    [Fact]
    public void Construction_SelectsRecordingAsStartupPage()
    {
        _ = _viewModel.SelectedTopItem.Should().BeSameAs(_viewModel.TopNavigationItems[0]);
        _ = _viewModel.SelectedBottomItem.Should().BeNull();
        _ = _viewModel.SelectedNavigationItem.Should().BeSameAs(_viewModel.TopNavigationItems[0]);
        _ = _viewModel.CurrentPage.Should().BeSameAs(_harness.Recording);
    }

    [Fact]
    public void Construction_WhenExtensionWarningWasPublishedBeforeSubscription_ShowsWarningBannerAndNotification()
    {
        var notifier = new FakeExtensionStatusNotifier();
        notifier.Publish(ExtensionStatusCode.Warning, "Please enable GNOME extension manually or restart your session");

        using var harness = MainWindowHarness.Create(extensionNotifier: notifier);
        var viewModel = harness.ViewModel;

        _ = viewModel.HasExtensionWarning.Should().BeTrue();
        _ = viewModel.ExtensionWarning.Should().Be("Please enable GNOME extension manually or restart your session");
        _ = viewModel.IsAppNotificationVisible.Should().BeTrue();
        _ = viewModel.AppNotificationTitle.Should().Be("[MainWindow_GnomeExtensionTitle]");
        _ = viewModel.AppNotificationMessage.Should().Be("Please enable GNOME extension manually or restart your session");
        _ = viewModel.IsAppNotificationWarning.Should().BeTrue();
    }

    [Fact]
    public void ExtensionStatusUpdated_WhenWarningPublishedAfterSubscription_ShowsWarningBannerAndNotification()
    {
        var notifier = new FakeExtensionStatusNotifier();
        using var harness = MainWindowHarness.Create(extensionNotifier: notifier);
        var viewModel = harness.ViewModel;

        notifier.Publish(ExtensionStatusCode.Warning, "GNOME extension requires logout/login to activate");

        _ = viewModel.HasExtensionWarning.Should().BeTrue();
        _ = viewModel.ExtensionWarning.Should().Be("GNOME extension requires logout/login to activate");
        _ = viewModel.IsAppNotificationVisible.Should().BeTrue();
        _ = viewModel.AppNotificationMessage.Should().Be("GNOME extension requires logout/login to activate");
        _ = viewModel.IsAppNotificationWarning.Should().BeTrue();
    }

    [Fact]
    public void Dispose_WhenAppNotificationTimerIsActive_DoesNotThrow()
    {
        var notifier = new FakeExtensionStatusNotifier();
        using var harness = MainWindowHarness.Create(extensionNotifier: notifier);
        var viewModel = harness.ViewModel;
        notifier.Publish(ExtensionStatusCode.Warning, "GNOME extension requires logout/login to activate");

        var act = viewModel.Dispose;

        _ = act.Should().NotThrow();
    }

    [Fact]
    public void ExtensionStatusUpdated_WhenErrorPublishedAfterSubscription_ShowsErrorNotification()
    {
        var notifier = new FakeExtensionStatusNotifier();
        using var harness = MainWindowHarness.Create(extensionNotifier: notifier);
        var viewModel = harness.ViewModel;

        notifier.Publish(ExtensionStatusCode.Error, "Failed to install GNOME extension");

        _ = viewModel.HasExtensionWarning.Should().BeTrue();
        _ = viewModel.ExtensionWarning.Should().Be("Failed to install GNOME extension");
        _ = viewModel.IsAppNotificationVisible.Should().BeTrue();
        _ = viewModel.AppNotificationMessage.Should().Be("Failed to install GNOME extension");
        _ = viewModel.IsAppNotificationError.Should().BeTrue();
        _ = viewModel.IsAppNotificationWarning.Should().BeFalse();
    }

    [Fact]
    public async Task InitializeAsync_StartsOwnedShellInitializationTask()
    {
        var schedulerGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var updateGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var schedulerService = Substitute.For<ISchedulerService>();
        _ = schedulerService.LoadAsync().Returns(async _ => await schedulerGate.Task);

        var updateService = Substitute.For<IUpdateService>();
        _ = updateService.CheckForUpdatesAsync(Arg.Any<CancellationToken>()).Returns(async unusedCallInfo =>
        {
            _ = await updateGate.Task;
            return new UpdateCheckResult
            {
                HasUpdate = true,
                LatestVersion = "9.9.9",
                ReleaseUrl = new Uri("https://example.invalid/releases/9.9.9"),
            };
        });

        using var harness = MainWindowHarness.Create(
            schedulerService: schedulerService,
            updateService: updateService,
            checkForUpdates: true);
        var viewModel = harness.ViewModel;

        _ = viewModel.InitializeAsync();
        _ = viewModel.StartupInitializationTask.IsCompleted.Should().BeFalse();

        schedulerGate.SetResult(true);
        await viewModel.InitializeAsync();
        await updateService.DidNotReceive().CheckForUpdatesAsync(Arg.Any<CancellationToken>());
        var optionalWork = viewModel.StartOptionalBackgroundWorkAsync(TestContext.Current.CancellationToken);
        Assert.False(optionalWork.IsCompleted);
        updateGate.SetResult(true);
        await optionalWork;

        await schedulerService.Received(1).LoadAsync();
        _ = await updateService.Received(1).CheckForUpdatesAsync(Arg.Any<CancellationToken>());
        _ = viewModel.LatestVersion.Should().Be("9.9.9");
        _ = viewModel.IsUpdateNotificationVisible.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_WhenScheduleInitializationHandlesFailure_ContinuesUpdateCheck()
    {
        var schedulerService = Substitute.For<ISchedulerService>();
        _ = schedulerService.LoadAsync().Returns(Task.FromException(new InvalidOperationException("scheduler boom")));

        var updateService = Substitute.For<IUpdateService>();
        _ = updateService.CheckForUpdatesAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new UpdateCheckResult
        {
            HasUpdate = true,
            LatestVersion = "1.2.3",
            ReleaseUrl = new Uri("https://example.invalid/releases/1.2.3"),
        }));

        using var harness = MainWindowHarness.Create(
            schedulerService: schedulerService,
            updateService: updateService,
            checkForUpdates: true);
        var viewModel = harness.ViewModel;

        await viewModel.InitializeAsync();
        await viewModel.StartOptionalBackgroundWorkAsync(TestContext.Current.CancellationToken);

        _ = schedulerService.Received(1).LoadAsync();
        _ = await updateService.Received(1).CheckForUpdatesAsync(Arg.Any<CancellationToken>());
        _ = viewModel.LatestVersion.Should().Be("1.2.3");
        _ = viewModel.IsUpdateNotificationVisible.Should().BeTrue();
    }

    [Fact]
    public async Task RecordingStateChanged_UpdatesPlaybackAvailability()
    {
        await _harness.Recording.StartRecordingAsync();

        _ = _harness.Playback.CanPlayMacroExternal.Should().BeFalse();

        _ = _harness.Recording.StopRecording();

        _ = _harness.Playback.CanPlayMacroExternal.Should().BeTrue();
    }

    [Fact]
    public async Task PlaybackStateChanged_UpdatesRecordingAvailabilityAndFileManagement()
    {
        _harness.Files.SetMacro(CreateMacro("Playback", EventType.MouseMove));
        var playbackStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowPlaybackCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = _harness.Player.PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
            .Returns(async unusedCallInfo =>
            {
                _ = playbackStarted.TrySetResult(true);
                _ = await allowPlaybackCompletion.Task;
            });

        var playTask = _harness.Playback.PlayMacroAsync();
        _ = await playbackStarted.Task.WaitAsync(TestTimeouts.Default, TimeProvider.System, CancellationToken.None);

        _ = _harness.Recording.CanStartRecordingExternal.Should().BeFalse();
        _ = _harness.Files.CanManageLoadedMacrosExternal.Should().BeFalse();

        _harness.Playback.StopPlayback();
        _ = allowPlaybackCompletion.TrySetResult(true);
        await playTask;

        _ = _harness.Recording.CanStartRecordingExternal.Should().BeTrue();
        _ = _harness.Files.CanManageLoadedMacrosExternal.Should().BeTrue();
    }

    [Fact]
    public async Task MacroLoaded_UpdatesGlobalStatusAndSharedMacroSelection()
    {
        var macro = new MacroSequence
        {
            Name = "TestMacro",
            Events = { new MacroEvent { Type = EventType.MouseMove } },
        };

        _ = _harness.FilesDialogService.ShowOpenFileDialogAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<FileDialogFilter>>())
            .Returns(Task.FromResult<string?>("/path/to/macro.macro"));
        _ = _harness.FileManager.LoadAsync("/path/to/macro.macro")
            .Returns(Task.FromResult<MacroSequence?>(macro));

        await _harness.Files.LoadMacroAsync();

        _ = _viewModel.GlobalStatus.Should().Be("[Status_LoadedMacro] TestMacro");
        _ = _harness.Files.CurrentMacro.Should().BeSameAs(macro);
        _ = _harness.Playback.HasMacro.Should().BeTrue();
        _ = _harness.Recording.EventCount.Should().Be(1);
        _ = _harness.Recording.RecordingStatus.Should().Be("[Recording_StatusLoadedEvents] 1");
    }

    [Fact]
    public void FilesSelectionChanged_WhenIdle_UpdatesRecordingCountersAndStatus()
    {
        var firstMacro = CreateMacro("First", EventType.MouseMove, EventType.KeyPress);
        var secondMacro = CreateMacro("Second", EventType.ButtonPress, EventType.ButtonRelease, EventType.MouseMove, EventType.KeyPress);

        _harness.Files.SetMacro(firstMacro);
        _harness.Files.SetMacro(secondMacro);

        _harness.Files.SelectedMacroItem = _harness.Files.LoadedMacros[0];
        _ = _harness.Recording.EventCount.Should().Be(2);
        _ = _harness.Recording.MouseEventCount.Should().Be(1);
        _ = _harness.Recording.KeyboardEventCount.Should().Be(1);
        _ = _harness.Recording.RecordingStatus.Should().Be("[Recording_StatusLoadedEvents] 2");

        _harness.Files.SelectedMacroItem = _harness.Files.LoadedMacros[1];
        _ = _harness.Recording.EventCount.Should().Be(4);
        _ = _harness.Recording.MouseEventCount.Should().Be(3);
        _ = _harness.Recording.KeyboardEventCount.Should().Be(1);
        _ = _harness.Recording.RecordingStatus.Should().Be("[Recording_StatusLoadedEvents] 4");
    }

    [Fact]
    public async Task FilesSelectionChanged_WhenRecordingActive_DoesNotOverwriteRecordingCounters()
    {
        var firstMacro = CreateMacro("First", EventType.MouseMove, EventType.KeyPress, EventType.KeyRelease);
        var secondMacro = CreateMacro("Second", EventType.ButtonPress, EventType.ButtonRelease, EventType.MouseMove, EventType.KeyPress);

        _harness.Files.SetMacro(firstMacro);
        _harness.Files.SetMacro(secondMacro);

        var firstItem = _harness.Files.LoadedMacros[0];
        var secondItem = _harness.Files.LoadedMacros[1];
        _harness.Files.SelectedMacroItem = firstItem;

        await _harness.Recording.StartRecordingAsync();
        PublishRecordedEvents(EventType.MouseMove, EventType.ButtonPress, EventType.ButtonRelease, EventType.KeyPress, EventType.KeyPress, EventType.KeyPress, EventType.KeyRelease);

        _harness.Files.SelectedMacroItem = secondItem;

        _ = _harness.Recording.EventCount.Should().Be(7);
        _ = _harness.Recording.MouseEventCount.Should().Be(3);
        _ = _harness.Recording.KeyboardEventCount.Should().Be(4);
        _ = _harness.Recording.StopRecording();
    }

    [Fact]
    public async Task RenameSelectedLoadedMacro_DoesNotRewriteRecordingStatusOrCounters()
    {
        var macro = CreateMacro("Original", Enumerable.Repeat(EventType.MouseMove, 40).Concat(Enumerable.Repeat(EventType.KeyPress, 59)).ToArray());
        _ = _harness.Recorder.StopRecording().Returns(macro);
        await _harness.Recording.StartRecordingAsync();
        PublishRecordedEvents(Enumerable.Repeat(EventType.MouseMove, 40).Concat(Enumerable.Repeat(EventType.KeyPress, 59)));
        _ = _harness.Recording.StopRecording();

        _harness.Files.MacroName = "Renamed Macro";

        _ = _harness.Files.SelectedMacroItem!.Name.Should().Be("Renamed Macro");
        _ = _harness.Recording.RecordingStatus.Should().Be("[Recording_StatusRecordedEvents] 99");
        _ = _harness.Recording.EventCount.Should().Be(99);
        _ = _harness.Recording.MouseEventCount.Should().Be(40);
        _ = _harness.Recording.KeyboardEventCount.Should().Be(59);
    }

    [Fact]
    public async Task RecordingCompleted_WhenMacroIsAutoSelected_PreservesRecordedStatus()
    {
        var recordedMacro = CreateMacro("RecordedMacro", EventType.MouseMove, EventType.KeyPress);
        _ = _harness.Recorder.StopRecording().Returns(recordedMacro);
        await _harness.Recording.StartRecordingAsync();
        PublishRecordedEvents(EventType.MouseMove, EventType.KeyPress);

        var result = _harness.Recording.StopRecording();

        _ = result.Should().BeSameAs(recordedMacro);
        _ = _harness.Files.CurrentMacro.Should().BeSameAs(recordedMacro);
        _ = _harness.Recording.RecordingStatus.Should().Be("[Recording_StatusRecordedEvents] 2");
    }

    [Fact]
    public async Task RemovingLastLoadedMacro_ClearsRecordingCounters()
    {
        var macro = CreateMacro("LoadedMacro", EventType.MouseMove, EventType.ButtonPress, EventType.KeyPress);
        _harness.Files.SetMacro(macro);
        _ = _harness.FilesDialogService.ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>(), "Yes", "No")
            .Returns(Task.FromResult(true));

        _ = _harness.Recording.EventCount.Should().Be(3);
        _ = _harness.Recording.MouseEventCount.Should().Be(2);
        _ = _harness.Recording.KeyboardEventCount.Should().Be(1);

        await _harness.Files.RemoveLoadedMacroCommand.ExecuteAsync(_harness.Files.SelectedMacroItem);

        _ = _harness.Recording.EventCount.Should().Be(0);
        _ = _harness.Recording.MouseEventCount.Should().Be(0);
        _ = _harness.Recording.KeyboardEventCount.Should().Be(0);
        _ = _harness.Recording.RecordingStatus.Should().Be("[Recording_StatusReady]");
    }

    [Fact]
    public async Task EditorMacroCreated_WhenSavingDifferentEditorDocument_DoesNotOverwriteSelectedLoadedMacro()
    {
        var editorMacro = CreateMacro("Editor Macro", EventType.MouseMove, EventType.KeyPress);
        var editorMacroUpdated = CreateMacro("Editor Macro Updated", EventType.ButtonPress, EventType.KeyPress);
        var unrelatedSelectedMacro = CreateMacro("Selected Macro", EventType.ButtonRelease, EventType.KeyRelease);

        await SaveEditorMacroAsync(editorMacro);
        var trackedEditorItem = _harness.Files.SelectedMacroItem;
        _ = trackedEditorItem.Should().NotBeNull();

        _harness.Files.SetMacro(unrelatedSelectedMacro);
        var selectedItem = _harness.Files.SelectedMacroItem;
        _ = selectedItem.Should().NotBeNull();
        _ = selectedItem.Should().NotBeSameAs(trackedEditorItem);

        await SaveEditorMacroAsync(editorMacroUpdated);

        _ = _harness.Files.LoadedMacros.Should().HaveCount(2);
        _ = trackedEditorItem.Macro.Should().BeSameAs(editorMacroUpdated);
        _ = trackedEditorItem.Name.Should().Be("Editor Macro Updated");
        _ = selectedItem.Macro.Should().BeSameAs(unrelatedSelectedMacro);
        _ = selectedItem.Name.Should().Be("Selected Macro");
        _ = _harness.Files.SelectedMacroItem.Should().BeSameAs(selectedItem);
    }

    [Fact]
    public async Task EditorMacroCreated_WhenSavingSameMacroAgain_DoesNotAppendDuplicateLoadedItem()
    {
        var firstMacro = CreateMacro("Editor Macro", EventType.MouseMove, EventType.KeyPress);
        var updatedMacro = CreateMacro("Editor Macro Updated", EventType.ButtonPress, EventType.ButtonRelease, EventType.KeyPress);

        await SaveEditorMacroAsync(firstMacro);
        var selectedItem = _harness.Files.SelectedMacroItem;
        _ = selectedItem.Should().NotBeNull();
        selectedItem.SequenceRepeatCount = 4;

        await SaveEditorMacroAsync(updatedMacro);

        _ = _harness.Files.LoadedMacros.Should().ContainSingle();
        _ = _harness.Files.SelectedMacroItem.Should().BeSameAs(selectedItem);
        _ = _harness.Files.SelectedMacroItem.Macro.Should().BeSameAs(updatedMacro);
        _ = _harness.Files.SelectedMacroItem.Name.Should().Be("Editor Macro Updated");
        _ = _harness.Files.SelectedMacroItem.SequenceRepeatCount.Should().Be(4);
    }

    [Fact]
    public async Task EditorMacroCreated_WhenSavePathChanges_UpdatesLoadedMacroSourcePath()
    {
        var firstMacro = CreateMacro("Editor Macro", EventType.MouseMove, EventType.KeyPress);
        var updatedMacro = CreateMacro("Editor Macro Updated", EventType.ButtonPress, EventType.KeyPress);

        await SaveEditorMacroAsync(firstMacro, "/tmp/editor-original.macro");
        var item = _harness.Files.SelectedMacroItem;

        _ = item.Should().NotBeNull();
        _ = item.SourcePath.Should().Be("/tmp/editor-original.macro");

        await SaveEditorMacroAsync(updatedMacro, "/tmp/editor-save-as.macro", saveAs: true);

        _ = _harness.Files.LoadedMacros.Should().ContainSingle();
        _ = item.Macro.Should().BeSameAs(updatedMacro);
        _ = item.SourcePath.Should().Be("/tmp/editor-save-as.macro");
        _ = item.Description.Should().Contain("editor-save-as.macro");
    }

    [Fact]
    public async Task EditorLoadedMacro_WhenExplicitlyAddedToPlayback_TracksTheSessionItemForSubsequentSaves()
    {
        const string sourcePath = "/tmp/editor-loaded.macro";
        var loadedMacro = CreateMacro("Loaded Macro", EventType.MouseMove);
        var addedMacro = CreateMacro("Loaded Macro", EventType.KeyPress);
        var updatedMacro = CreateMacro("Updated Macro", EventType.ButtonPress, EventType.KeyPress);
        _ = _harness.EditorConverter.FromMacroSequenceWithDiagnostics(loadedMacro)
            .Returns(new EditorActionRestoreResult([new EditorAction { Type = EditorActionType.MouseClick }], [], restoredFromScriptSteps: true));
        _ = _harness.EditorValidator.ValidateAll(Arg.Any<IEnumerable<EditorAction>>()).Returns((true, new List<string>()));
        _ = _harness.EditorConverter.ToMacroSequence(Arg.Any<EditorMacroProjection>()).Returns(addedMacro);
        var document = _viewModel.Editor.ActiveDocument!;
        document.LoadMacroSequence(loadedMacro, sourcePath);

        await document.AddToPlaybackAsync();

        var item = _harness.Files.SelectedMacroItem;
        _ = item.Should().NotBeNull();
        _ = item.Macro.Should().BeSameAs(addedMacro);
        _ = item.SourcePath.Should().Be(sourcePath);
        _ = document.LinkedLoadedMacroSessionId.Should().Be(item.SessionId);
        _ = document.CanAddToPlayback.Should().BeFalse();

        _ = _harness.EditorConverter.ToMacroSequence(Arg.Any<EditorMacroProjection>()).Returns(updatedMacro);
        await SaveEditorMacroAsync(updatedMacro, sourcePath);

        _ = _harness.Files.LoadedMacros.Should().ContainSingle();
        _ = item.Macro.Should().BeSameAs(updatedMacro);
    }

    [Fact]
    public async Task EditorLinkedMacro_WhenRemovedFromPlayback_DetachesAndSaveDoesNotAddItBack()
    {
        const string sourcePath = "/tmp/editor-detached.macro";
        var loadedMacro = CreateMacro("Loaded Macro", EventType.MouseMove);
        var playbackMacro = CreateMacro("Loaded Macro", EventType.KeyPress);
        var savedMacro = CreateMacro("Saved Macro", EventType.ButtonPress);
        _ = _harness.EditorConverter.FromMacroSequenceWithDiagnostics(loadedMacro)
            .Returns(new EditorActionRestoreResult(
                [new EditorAction { Type = EditorActionType.MouseClick }],
                [],
                restoredFromScriptSteps: true));
        _ = _harness.EditorValidator.ValidateAll(Arg.Any<IEnumerable<EditorAction>>()).Returns((true, new List<string>()));
        _ = _harness.EditorConverter.ToMacroSequence(Arg.Any<EditorMacroProjection>()).Returns(playbackMacro);
        var document = _viewModel.Editor.ActiveDocument!;
        document.LoadMacroSequence(loadedMacro, sourcePath);
        await document.AddToPlaybackAsync();
        var item = _harness.Files.SelectedMacroItem!;
        _ = _harness.FilesDialogService.ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>(), "Yes", "No")
            .Returns(Task.FromResult(true));

        await _harness.Files.RemoveLoadedMacroCommand.ExecuteAsync(item);

        _ = document.LinkedLoadedMacroSessionId.Should().BeNull();
        _ = document.CanAddToPlayback.Should().BeTrue();
        _ = _harness.EditorConverter.ToMacroSequence(Arg.Any<EditorMacroProjection>()).Returns(savedMacro);
        _ = _harness.FileManager.SaveAsync(savedMacro, sourcePath).Returns(Task.CompletedTask);
        await document.SaveMacroAsync();

        _ = _harness.Files.LoadedMacros.Should().BeEmpty();
        _ = document.LinkedLoadedMacroSessionId.Should().BeNull();
        _ = document.CanAddToPlayback.Should().BeTrue();
    }

    [Fact]
    public async Task EditorLoadedMacro_WhenSavedWithoutExplicitPlaybackAdd_DoesNotAddSessionItem()
    {
        const string sourcePath = "/tmp/editor-disk-only.macro";
        var loadedMacro = CreateMacro("Loaded Macro", EventType.MouseMove);
        var savedMacro = CreateMacro("Saved Macro", EventType.KeyPress);
        _ = _harness.EditorConverter.FromMacroSequenceWithDiagnostics(loadedMacro)
            .Returns(new EditorActionRestoreResult(
                [new EditorAction { Type = EditorActionType.MouseClick }],
                [],
                restoredFromScriptSteps: true));
        _ = _harness.EditorValidator.ValidateAll(Arg.Any<IEnumerable<EditorAction>>()).Returns((true, new List<string>()));
        _ = _harness.EditorConverter.ToMacroSequence(Arg.Any<EditorMacroProjection>()).Returns(savedMacro);
        _ = _harness.FileManager.SaveAsync(savedMacro, sourcePath).Returns(Task.CompletedTask);
        var document = _viewModel.Editor.ActiveDocument!;
        document.LoadMacroSequence(loadedMacro, sourcePath);

        await document.SaveMacroAsync();

        _ = _harness.Files.LoadedMacros.Should().BeEmpty();
        _ = document.CanAddToPlayback.Should().BeTrue();
    }

    [Fact]
    public async Task EditorSave_WhenFileWasOpenedIndependently_RefreshesAllMatchingPlaybackRowsWithoutChangingPlaylist()
    {
        var sourcePath = Path.Combine(Path.GetTempPath(), "editor-sync.macro");
        var original = CreateMacro("Original", EventType.MouseMove);
        original.Events[0] = new MacroEvent { Type = EventType.MouseMove, X = 10 };
        var first = _harness.LoadedMacroSession.AddMacro(original.Clone(), sourcePath);
        first.SequenceRepeatCount = 2;
        var unrelated = _harness.LoadedMacroSession.AddMacro(CreateMacro("Other", EventType.KeyPress), sourcePath + ".other");
        unrelated.SequenceRepeatCount = 5;
        var duplicate = _harness.LoadedMacroSession.AddMacro(original.Clone(), Path.Combine(Path.GetTempPath(), ".", "editor-sync.macro"));
        duplicate.SequenceRepeatCount = 7;
        _harness.LoadedMacroSession.SelectedMacroItem = unrelated;
        var sessionIds = _harness.LoadedMacroSession.LoadedMacros.Select(item => item.SessionId).ToArray();
        var converter = UseRealEditorConversion();
        _harness.Editor.LoadMacroSequence(original, sourcePath);
        _harness.Editor.Actions.Add(converter.FromMacroEvent(new MacroEvent { Type = EventType.MouseMove, X = 100 }));

        _ = first.Macro.Events.Should().ContainSingle();
        _ = duplicate.Macro.Events.Should().ContainSingle();
        await _harness.Editor.SaveMacroAsync();

        _ = _harness.LoadedMacroSession.LoadedMacros.Select(item => item.SessionId).Should().Equal(sessionIds);
        _ = _harness.LoadedMacroSession.LoadedMacros.Select(item => item.SequenceRepeatCount).Should().Equal(2, 5, 7);
        _ = first.Macro.Events.Select(ev => ev.X).Should().Equal(10, 100);
        _ = duplicate.Macro.Events.Select(ev => ev.X).Should().Equal(10, 100);
        _ = unrelated.Macro.Events.Select(ev => ev.Type).Should().Equal(EventType.KeyPress);
        _ = _harness.LoadedMacroSession.SelectedMacroItem.Should().BeSameAs(unrelated);
        _ = _harness.Editor.LinkedLoadedMacroSessionId.Should().Be(first.SessionId);

        duplicate.Name = "Repeated copy";
        _ = first.Macro.Name.Should().Be("Original");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EditorAddToPlayback_WhenFileIsAlreadyLoaded_ReusesAndSelectsExistingRow(bool matchingRowIsSelected)
    {
        var sourcePath = Path.Combine(Path.GetTempPath(), "editor-add-existing.macro");
        var original = CreateMacro("Original", EventType.MouseMove);
        original.Events[0] = new MacroEvent { Type = EventType.MouseMove, X = 10 };
        var first = _harness.LoadedMacroSession.AddMacro(original.Clone(), sourcePath);
        first.SequenceRepeatCount = 2;
        var duplicate = _harness.LoadedMacroSession.AddMacro(original.Clone(), sourcePath);
        duplicate.SequenceRepeatCount = 4;
        var unrelated = _harness.LoadedMacroSession.AddMacro(CreateMacro("Other", EventType.KeyPress));
        _harness.LoadedMacroSession.SelectedMacroItem = matchingRowIsSelected ? duplicate : unrelated;
        var expectedItem = matchingRowIsSelected ? duplicate : first;
        var sessionIds = _harness.LoadedMacroSession.LoadedMacros.Select(item => item.SessionId).ToArray();
        var converter = UseRealEditorConversion();
        _harness.Editor.LoadMacroSequence(original, sourcePath);
        _harness.Editor.Actions.Add(converter.FromMacroEvent(new MacroEvent { Type = EventType.MouseMove, X = 100 }));

        await _harness.Editor.AddToPlaybackAsync();

        _ = _harness.LoadedMacroSession.LoadedMacros.Select(item => item.SessionId).Should().Equal(sessionIds);
        _ = _harness.LoadedMacroSession.LoadedMacros.Select(item => item.SequenceRepeatCount).Should().Equal(2, 4, 1);
        _ = _harness.LoadedMacroSession.SelectedMacroItem.Should().BeSameAs(expectedItem);
        _ = expectedItem.Macro.Events.Select(ev => ev.X).Should().Equal(10, 100);
        _ = _harness.Editor.LinkedLoadedMacroSessionId.Should().Be(expectedItem.SessionId);
        _ = _harness.Editor.CanAddToPlayback.Should().BeFalse();
    }

    [Fact]
    public async Task EditorSaveAs_WhenDestinationIsAlreadyLoaded_RefreshesDestinationWithoutChangingOtherSourceCopies()
    {
        var sourcePath = Path.Combine(Path.GetTempPath(), "editor-save-source.macro");
        var destinationPath = Path.Combine(Path.GetTempPath(), "editor-save-destination.macro");
        var original = CreateMacro("Original", EventType.MouseMove);
        await SaveEditorMacroAsync(original, sourcePath);
        var linked = _harness.LoadedMacroSession.SelectedMacroItem!;
        var sourceCopy = _harness.LoadedMacroSession.AddMacro(original.Clone(), sourcePath);
        var destination = _harness.LoadedMacroSession.AddMacro(CreateMacro("Destination", EventType.KeyPress), destinationPath);
        destination.SequenceRepeatCount = 3;
        var sessionIds = _harness.LoadedMacroSession.LoadedMacros.Select(item => item.SessionId).ToArray();
        var converter = UseRealEditorConversion();
        _harness.Editor.Actions.Clear();
        _harness.Editor.Actions.Add(converter.FromMacroEvent(new MacroEvent { Type = EventType.MouseMove, X = 100 }));
        _ = _harness.EditorDialogService.ShowSaveFileDialogAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<FileDialogFilter>>())
            .Returns(destinationPath);

        await _harness.Editor.SaveMacroAsAsync();

        _ = _harness.LoadedMacroSession.LoadedMacros.Select(item => item.SessionId).Should().Equal(sessionIds);
        _ = linked.SourcePath.Should().Be(destinationPath);
        _ = linked.Macro.Events.Select(ev => ev.X).Should().Equal(100);
        _ = destination.Macro.Events.Select(ev => ev.X).Should().Equal(100);
        _ = destination.SequenceRepeatCount.Should().Be(3);
        _ = sourceCopy.SourcePath.Should().Be(sourcePath);
        _ = sourceCopy.Macro.Events.Select(ev => ev.X).Should().Equal(0);
        _ = _harness.LoadedMacroSession.SelectedMacroItem.Should().BeSameAs(destination);
        _ = _harness.Editor.LinkedLoadedMacroSessionId.Should().Be(linked.SessionId);
    }

    [Fact]
    public async Task EditorMacroCreated_WhenMacroHasOnlyScreenReadingScriptSteps_ReportsActionCount()
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

        await SaveEditorMacroAsync(macro);

        _ = _viewModel.GlobalStatus.Should().Be("[Status_CreatedMacro] Screen Reading Macro (3)");
        _ = _harness.Files.SelectedMacroItem!.EventCount.Should().Be(3);
    }

    [Fact]
    public async Task StopPlayback_WhenSequenceCleanupStillRunning_KeepsFilesLockedUntilPlaybackTaskFinishes()
    {
        var first = CreateMacro("First", EventType.MouseMove);
        var second = CreateMacro("Second", EventType.KeyPress);
        _harness.Files.SetMacro(first);
        _harness.Files.SetMacro(second);
        _harness.Files.IsSequentialCycleMode = true;
        _harness.Files.SelectedMacroItem = _harness.Files.LoadedMacros[0];

        var playStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = _harness.Player.PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
            .Returns(async unusedCallInfo =>
            {
                _ = playStarted.TrySetResult(true);
                _ = await allowCompletion.Task;
            });

        var playTask = _harness.Playback.PlayMacroAsync();
        _ = await playStarted.Task.WaitAsync(TestTimeouts.Default, TimeProvider.System, CancellationToken.None);

        _ = _harness.Files.CanManageLoadedMacrosExternal.Should().BeFalse();

        _harness.Playback.StopPlayback();

        _ = _harness.Files.CanManageLoadedMacrosExternal.Should().BeFalse();

        _ = allowCompletion.TrySetResult(true);
        await playTask;

        _ = _harness.Files.CanManageLoadedMacrosExternal.Should().BeTrue();
    }

    [Fact]
    public async Task StopPlayback_WhenCompletionOccursOffThread_ReenablesRecordingAndFilesOnUiExecutorAfterCleanup()
    {
        var scenario = new DeferredUiScenario(_harness.Player, _harness.UiDispatcher);
        _harness.Files.SetMacro(CreateMacro("Playback", EventType.MouseMove));
        _harness.Playback.CanPlayMacroExternal = true;
        var availabilityChanges = new List<(string PropertyName, bool Value, SynchronizationContext? Context)>();
        _harness.Recording.PropertyChanged += (_, args) =>
        {
            if (string.Equals(args.PropertyName, nameof(RecordingViewModel.CanStartRecordingExternal), StringComparison.Ordinal))
            {
                availabilityChanges.Add((args.PropertyName!, _harness.Recording.CanStartRecordingExternal, SynchronizationContext.Current));
            }
        };
        _harness.Files.PropertyChanged += (_, args) =>
        {
            if (string.Equals(args.PropertyName, nameof(FilesViewModel.CanManageLoadedMacrosExternal), StringComparison.Ordinal))
            {
                availabilityChanges.Add((args.PropertyName!, _harness.Files.CanManageLoadedMacrosExternal, SynchronizationContext.Current));
            }
        };

        var playTask = await scenario.StartAsync(_harness.Playback.PlayMacroAsync);
        _ = _harness.Recording.CanStartRecordingExternal.Should().BeFalse();
        _ = _harness.Files.CanManageLoadedMacrosExternal.Should().BeFalse();
        availabilityChanges.Clear();

        _harness.Playback.StopPlayback();
        await scenario.CompleteOffThreadAsync();
        _ = _harness.Recording.CanStartRecordingExternal.Should().BeFalse();
        _ = _harness.Files.CanManageLoadedMacrosExternal.Should().BeFalse();

        await scenario.ReleasePlayerAsync(playTask);
        _ = _harness.Recording.CanStartRecordingExternal.Should().BeFalse();
        _ = _harness.Files.CanManageLoadedMacrosExternal.Should().BeFalse();

        await scenario.DrainUiAsync(playTask);

        _ = _harness.Recording.CanStartRecordingExternal.Should().BeTrue();
        _ = _harness.Files.CanManageLoadedMacrosExternal.Should().BeTrue();
        _ = availabilityChanges.Select(change => (change.PropertyName, change.Value)).Should().Equal(
            (nameof(RecordingViewModel.CanStartRecordingExternal), true),
            (nameof(FilesViewModel.CanManageLoadedMacrosExternal), true));
        _ = availabilityChanges.Should().OnlyContain(change => ReferenceEquals(change.Context, scenario.Executor));
    }

    [Fact]
    public void OpenUpdateUrl_AlwaysDismissesNotification()
    {
        _viewModel.IsUpdateNotificationVisible = true;

        _viewModel.OpenUpdateUrl();

        _ = _viewModel.IsUpdateNotificationVisible.Should().BeFalse();
    }

    [Fact]
    public void DismissUpdateNotificationCommand_ExecutesBoundDismissAction()
    {
        _viewModel.IsUpdateNotificationVisible = true;

        _viewModel.DismissUpdateNotificationCommand.Execute(parameter: null);

        _ = _viewModel.IsUpdateNotificationVisible.Should().BeFalse();
    }

    [Fact]
    public async Task OpenUpdateUrlCommand_ExecutesBoundOpenActionAndDismissesNotification()
    {
        var updateService = Substitute.For<IUpdateService>();
        _ = updateService.CheckForUpdatesAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new UpdateCheckResult
        {
            HasUpdate = true,
            LatestVersion = "9.9.9",
            ReleaseUrl = new Uri("https://example.invalid/releases/latest", UriKind.Absolute),
        }));
        var opened = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
        var externalUrlOpener = Substitute.For<IExternalUrlOpener>();
        _ = externalUrlOpener.OpenAsync(Arg.Any<Uri>()).Returns(callInfo =>
        {
            _ = opened.TrySetResult(callInfo.Arg<Uri>());
            return Task.CompletedTask;
        });
        using var harness = MainWindowHarness.Create(
            updateService: updateService,
            checkForUpdates: true,
            externalUrlOpener: externalUrlOpener);
        var viewModel = harness.ViewModel;
        await viewModel.InitializeAsync();

        await viewModel.StartOptionalBackgroundWorkAsync(TestContext.Current.CancellationToken);
        viewModel.OpenUpdateUrlCommand.Execute(parameter: null);

        _ = (await opened.Task.WaitAsync(TestTimeouts.Default, TimeProvider.System, CancellationToken.None)).Should().Be(new Uri("https://example.invalid/releases/latest", UriKind.Absolute));
        await externalUrlOpener.Received(1).OpenAsync(new Uri("https://example.invalid/releases/latest", UriKind.Absolute));
        _ = viewModel.IsUpdateNotificationVisible.Should().BeFalse();
    }

    [Fact]
    public async Task StartupInitialization_WhenPlatformStartupNotificationAvailable_ShowsDismissibleWarningNotification()
    {
        var platformNotificationProvider = Substitute.For<IPlatformStartupNotificationProvider>();
        _ = platformNotificationProvider.GetStartupNotification().Returns(new PlatformStartupNotification(
            "Platform Compatibility",
            "Platform startup warning is active.",
            PlatformStartupNotificationSeverity.Warning));

        using var harness = MainWindowHarness.Create(platformStartupNotificationProviders: [platformNotificationProvider]);
        var viewModel = harness.ViewModel;

        await viewModel.InitializeAsync();

        _ = viewModel.IsAppNotificationVisible.Should().BeTrue();
        _ = viewModel.AppNotificationTitle.Should().Be("Platform Compatibility");
        _ = viewModel.AppNotificationMessage.Should().Be("Platform startup warning is active.");
        _ = viewModel.IsAppNotificationWarning.Should().BeTrue();

        viewModel.DismissAppNotification();

        _ = viewModel.IsAppNotificationVisible.Should().BeFalse();
    }

    [Fact]
    public async Task StartupInitialization_WhenExtensionWarningAlreadyVisible_DoesNotReplaceItWithPlatformNotification()
    {
        var notifier = new FakeExtensionStatusNotifier();
        notifier.Publish(ExtensionStatusCode.Warning, "GNOME extension requires logout/login to activate");
        var platformNotificationProvider = Substitute.For<IPlatformStartupNotificationProvider>();
        _ = platformNotificationProvider.GetStartupNotification().Returns(new PlatformStartupNotification(
            "Platform Compatibility",
            "Platform startup warning is active.",
            PlatformStartupNotificationSeverity.Warning));

        using var harness = MainWindowHarness.Create(
            extensionNotifier: notifier,
            platformStartupNotificationProviders: [platformNotificationProvider]);
        var viewModel = harness.ViewModel;

        await viewModel.InitializeAsync();

        _ = viewModel.HasExtensionWarning.Should().BeTrue();
        _ = viewModel.ExtensionWarning.Should().Be("GNOME extension requires logout/login to activate");
        _ = viewModel.AppNotificationTitle.Should().Be("[MainWindow_GnomeExtensionTitle]");
        _ = viewModel.AppNotificationMessage.Should().Be("GNOME extension requires logout/login to activate");
        _ = viewModel.IsAppNotificationWarning.Should().BeTrue();
        _ = platformNotificationProvider.DidNotReceive().GetStartupNotification();
    }

    [Fact]
    public async Task StartupInitialization_WhenPlatformStartupNotificationProviderThrows_SkipsProvider()
    {
        var throwingProvider = Substitute.For<IPlatformStartupNotificationProvider>();
        _ = throwingProvider.GetStartupNotification().Returns(_ => throw new InvalidOperationException("provider failed"));
        var workingProvider = Substitute.For<IPlatformStartupNotificationProvider>();
        _ = workingProvider.GetStartupNotification().Returns(new PlatformStartupNotification(
            "Platform Compatibility",
            "Platform startup warning is active.",
            PlatformStartupNotificationSeverity.Warning));

        using var harness = MainWindowHarness.Create(
            platformStartupNotificationProviders: [throwingProvider, workingProvider]);
        var viewModel = harness.ViewModel;

        await viewModel.InitializeAsync();

        _ = viewModel.IsAppNotificationVisible.Should().BeTrue();
        _ = viewModel.AppNotificationTitle.Should().Be("Platform Compatibility");
        _ = viewModel.AppNotificationMessage.Should().Be("Platform startup warning is active.");
    }

    [Fact]
    public void CultureChanged_RefreshesNavigationLabels_ByLocalizationKey()
    {
        _ = _viewModel.TopNavigationItems[0].LocalizationKey.Should().Be("Navigation_Recording");
        _ = _viewModel.BottomNavigationItems[0].LocalizationKey.Should().Be("Navigation_Settings");

        _ = _harness.LocalizationService["Navigation_Recording"].Returns("[Navigation_Recording:updated]");
        _ = _harness.LocalizationService["Navigation_Settings"].Returns("[Navigation_Settings:updated]");

        _harness.LocalizationService.CultureChanged += Raise.Event<EventHandler>(_harness.LocalizationService, EventArgs.Empty);

        _ = _viewModel.TopNavigationItems[0].Label.Should().Be("[Navigation_Recording:updated]");
        _ = _viewModel.BottomNavigationItems[0].Label.Should().Be("[Navigation_Settings:updated]");
    }

    [Fact]
    public void CultureChanged_WhenIdleAndNoMacro_RefreshesGlobalReadyStatus()
    {
        _viewModel.GlobalStatus = "[Status_Ready]";
        _ = _harness.LocalizationService["Status_Ready"].Returns("[Status_Ready:updated]");

        _harness.LocalizationService.CultureChanged += Raise.Event<EventHandler>(_harness.LocalizationService, EventArgs.Empty);

        _ = _viewModel.GlobalStatus.Should().Be("[Status_Ready:updated]");
    }

    [Fact]
    public void CultureChanged_WhenIdleWithMacro_RefreshesGlobalStatusFromRecordingSummary()
    {
        var macro = CreateMacro("Macro", EventType.MouseMove, EventType.KeyPress);
        _harness.Files.SetMacro(macro);
        _ = _harness.LocalizationService["Recording_StatusLoadedEvents"].Returns("[Recording_StatusLoadedEvents:updated] {0}");

        _harness.LocalizationService.CultureChanged += Raise.Event<EventHandler>(_harness.LocalizationService, EventArgs.Empty);

        _ = _viewModel.GlobalStatus.Should().Be("[Recording_StatusLoadedEvents:updated] 2");
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var act = () =>
        {
            _viewModel.Dispose();
            _viewModel.Dispose();
        };

        _ = act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_UnsubscribesShellButLeavesDependencyOwnedChildrenUsable()
    {
        _viewModel.Dispose();
        var changedProperties = new List<string?>();
        _viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);
        _harness.Files.SetMacro(new MacroSequence { Name = "after shell close" });
        _harness.LocalizationService.CultureChanged += Raise.Event<EventHandler>(_harness.LocalizationService, EventArgs.Empty);
        Assert.Empty(changedProperties);
        Assert.NotEmpty(_viewModel.Editor.Documents);
        Assert.Equal("after shell close", _harness.Files.CurrentMacro?.Name, StringComparer.Ordinal);
    }

    public void Dispose() => _harness.Dispose();

    private sealed class FakeExtensionStatusNotifier : IExtensionStatusNotifier
    {
        public event EventHandler<ExtensionStatusChangedEventArgs>? ExtensionStatusUpdated;

        public ExtensionStatusChangedEventArgs? CurrentExtensionStatus { get; private set; }

        public void Publish(ExtensionStatusCode code, string message)
        {
            var args = new ExtensionStatusChangedEventArgs(code, message);
            CurrentExtensionStatus = args;
            ExtensionStatusUpdated?.Invoke(this, args);
        }
    }

    private static MacroSequence CreateMacro(string name, params EventType[] eventTypes)
    {
        var macro = new MacroSequence { Name = name };
        foreach (var eventType in eventTypes)
        {
            macro.Events.Add(new MacroEvent { Type = eventType });
        }

        return macro;
    }

    private void PublishRecordedEvents(IEnumerable<EventType> eventTypes)
    {
        foreach (var eventType in eventTypes)
        {
            _harness.Recorder.EventRecorded += Raise.Event<EventHandler<MacroEventRecordedEventArgs>>(
                _harness.Recorder,
                new MacroEventRecordedEventArgs(new MacroEvent { Type = eventType }));
        }
    }

    private void PublishRecordedEvents(params EventType[] eventTypes)
    {
        PublishRecordedEvents((IEnumerable<EventType>)eventTypes);
    }

    private EditorActionConverter UseRealEditorConversion()
    {
        var converter = new EditorActionConverter(Substitute.For<IKeyCodeMapper>());
        var validator = new EditorActionValidator(converter);
        _ = _harness.EditorConverter.ToMacroSequence(Arg.Any<EditorMacroProjection>())
            .Returns(call => converter.ToMacroSequence(call.Arg<EditorMacroProjection>()));
        _ = _harness.EditorConverter.FromMacroSequenceWithDiagnostics(Arg.Any<MacroSequence>())
            .Returns(call => converter.FromMacroSequenceWithDiagnostics(call.Arg<MacroSequence>()));
        _ = _harness.EditorValidator.ValidateAll(Arg.Any<IEnumerable<EditorAction>>())
            .Returns(call => validator.ValidateAll(call.Arg<IEnumerable<EditorAction>>()));
        _ = _harness.FileManager.SaveAsync(Arg.Any<MacroSequence>(), Arg.Any<string>()).Returns(Task.CompletedTask);
        return converter;
    }

    private async Task SaveEditorMacroAsync(
        MacroSequence macro,
        string sourcePath = "/tmp/editor-test.macro",
        bool saveAs = false)
    {
        if (_harness.Editor.Actions.Count is 0)
        {
            _harness.Editor.AddAction();
        }

        _ = _harness.EditorValidator.ValidateAll(Arg.Any<IEnumerable<EditorAction>>())
            .Returns((true, new List<string>()));
        _ = _harness.EditorConverter.ToMacroSequence(Arg.Any<EditorMacroProjection>()).Returns(macro);
        _ = _harness.EditorDialogService.ShowSaveFileDialogAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<FileDialogFilter>>())
            .Returns(sourcePath);
        _ = _harness.FileManager.SaveAsync(macro, sourcePath).Returns(Task.CompletedTask);

        if (saveAs)
        {
            await _harness.Editor.SaveMacroAsAsync();
            return;
        }

        await _harness.Editor.SaveMacroAsync();
    }

}
