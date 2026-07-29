
namespace CrossMacro.UI.Tests.ViewModels;

public sealed class FilesViewModelTests
{
    private readonly IMacroFileManager _fileManager;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;
    private readonly LoadedMacroSession _loadedMacroSession;
    private readonly FilesViewModel _viewModel;

    public FilesViewModelTests()
    {
        _fileManager = Substitute.For<IMacroFileManager>();
        _dialogService = Substitute.For<IDialogService>();
        _localizationService = Substitute.For<ILocalizationService>();
        _ = _localizationService.CurrentCulture.Returns(System.Globalization.CultureInfo.GetCultureInfo("en"));
        _ = _localizationService[Arg.Any<string>()].Returns(call => call.Arg<string>() switch
        {
            "Files_StatusReady" => "[Files_StatusReady]",
            "Files_UnnamedMacro" => "[Files_UnnamedMacro]",
            "Files_SourceSession" => "[Files_SourceSession]",
            "Files_SequenceRepeatSummary" => "[Files_SequenceRepeatSummary] {0}",
            "Files_LoadedMacroDescription" => "[Files_LoadedMacroDescription] {0} | {1}",
            "Files_StatusSaveCancelled" => "[Files_StatusSaveCancelled]",
            "Files_StatusLoadCancelled" => "[Files_StatusLoadCancelled]",
            "Files_StatusRemoved" => "[Files_StatusRemoved] {0}",
            "Files_StatusSavedTo" => "[Files_StatusSavedTo] {0}",
            "Files_StatusLoaded" => "[Files_StatusLoaded] {0}",
            "Files_StatusSaveError" => "[Files_StatusSaveError] {0}",
            "Files_StatusLoadError" => "[Files_StatusLoadError] {0}",
            "Files_StatusLoadUnreadable" => "[Files_StatusLoadUnreadable]",
            "Files_OpenMacroDialogFilter" => "[Files_OpenMacroDialogFilter]",
            "Files_SaveDialogTitle" => "[Files_SaveDialogTitle]",
            "Files_LoadDialogTitle" => "[Files_LoadDialogTitle]",
            "Files_DeleteLoadedMacroTitle" => "[Files_DeleteLoadedMacroTitle]",
            "Files_DeleteLoadedMacroMessage" => "[Files_DeleteLoadedMacroMessage] {0}",
            _ => call.Arg<string>(),
        });
        _loadedMacroSession = new LoadedMacroSession(_localizationService);
        _viewModel = new FilesViewModel(_fileManager, _dialogService, _loadedMacroSession, _localizationService);
    }

    [Fact]
    public void Construction_InitializesProperties()
    {
        _ = _viewModel.Status.Should().Be("[Files_StatusReady]");
        _ = _viewModel.MacroName.Should().Be("New Macro");
        _ = _viewModel.SelectedSequenceRepeatCount.Should().Be(1);
        _ = _viewModel.HasRecordedMacro.Should().BeFalse();
        _ = _viewModel.HasLoadedMacros.Should().BeFalse();
        _ = _viewModel.CurrentMacro.Should().BeNull();
        _ = _viewModel.IsSelectedOnlyMode.Should().BeTrue();
        _ = _viewModel.ShowSequenceRepeatSettings.Should().BeFalse();
    }

    [Fact]
    public void SetMacro_AddsMacroToSessionAndSelectsIt()
    {
        var macro = CreateMacro("Test Macro");

        _viewModel.SetMacro(macro);

        _ = _viewModel.CurrentMacro.Should().BeSameAs(macro);
        _ = _viewModel.HasRecordedMacro.Should().BeTrue();
        _ = _viewModel.HasLoadedMacros.Should().BeTrue();
        _ = _viewModel.MacroName.Should().Be("Test Macro");
        _ = _viewModel.SelectedSequenceRepeatCount.Should().Be(1);
        _ = _viewModel.LoadedMacros.Should().HaveCount(1);
        _ = _viewModel.SelectedMacroItem.Should().NotBeNull();
        _ = _viewModel.SelectedMacroItem!.Macro.Should().BeSameAs(macro);
    }

    [Fact]
    public void SetMacro_WhenOnlyScreenReadingScriptSteps_AllowsSave()
    {
        var macro = new MacroSequence
        {
            Name = "Screen Reading Macro",
            ScriptSteps = { "pixelcolor 10 20 color" },
        };

        _viewModel.SetMacro(macro);

        _ = _viewModel.HasRecordedMacro.Should().BeTrue();
        _ = _viewModel.CanSaveMacro.Should().BeTrue();
        _ = _viewModel.SelectedMacroItem!.EventCount.Should().Be(1);
    }

    [Fact]
    public void SetMacro_WhenRecorderUsesDefaultPlaceholder_AppliesCurrentMacroName()
    {
        _viewModel.MacroName = "Recorded Macro";
        var macro = CreateMacro("New Macro");

        _viewModel.SetMacro(macro);

        _ = macro.Name.Should().Be("Recorded Macro");
        _ = _viewModel.SelectedMacroItem!.Name.Should().Be("Recorded Macro");
        _ = _viewModel.MacroName.Should().Be("Recorded Macro");
    }

    [Fact]
    public void SetMacro_WhenAnotherLoadedMacroIsSelected_DoesNotReuseSelectedNameForNewRecording()
    {
        _viewModel.SetMacro(CreateMacro("Existing Macro"));
        var recordedMacro = CreateMacro("New Macro");

        _viewModel.SetMacro(recordedMacro);

        _ = _viewModel.LoadedMacros.Should().HaveCount(2);
        _ = _viewModel.LoadedMacros[0].Name.Should().Be("Existing Macro");
        _ = _viewModel.LoadedMacros[1].Name.Should().Be("New Macro");
        _ = recordedMacro.Name.Should().Be("New Macro");
    }

    [Fact]
    public void SetMacro_AddsToSessionWithoutReplacingExistingLoadedMacros()
    {
        var firstMacro = CreateMacro("First Macro");
        var secondMacro = CreateMacro("Second Macro");

        _viewModel.SetMacro(firstMacro);
        _viewModel.SetMacro(secondMacro);

        _ = _viewModel.LoadedMacros.Should().HaveCount(2);
        _ = _viewModel.LoadedMacros[0].Macro.Should().BeSameAs(firstMacro);
        _ = _viewModel.LoadedMacros[1].Macro.Should().BeSameAs(secondMacro);
        _ = _viewModel.SelectedMacroItem.Should().BeSameAs(_viewModel.LoadedMacros[1]);
        _ = _viewModel.CurrentMacro.Should().BeSameAs(secondMacro);
    }

    [Fact]
    public void UpsertMacro_WhenTrackedSessionIdMatchesNonSelectedItem_UpdatesThatItemWithoutChangingSelection()
    {
        var trackedOriginal = CreateMacro("Tracked Original");
        var selectedOther = CreateMacro("Selected Other");
        _viewModel.SetMacro(trackedOriginal);
        var trackedItem = _viewModel.SelectedMacroItem;
        _viewModel.SetMacro(selectedOther);
        var selectedItem = _viewModel.SelectedMacroItem;
        var updatedTracked = CreateMacro("Tracked Updated");

        var result = _viewModel.UpsertMacro(trackedItem!.SessionId, updatedTracked);

        _ = result.Should().BeSameAs(trackedItem);
        _ = _viewModel.LoadedMacros.Should().HaveCount(2);
        _ = trackedItem.Macro.Should().BeSameAs(updatedTracked);
        _ = trackedItem.Name.Should().Be("Tracked Updated");
        _ = _viewModel.SelectedMacroItem.Should().BeSameAs(selectedItem);
        _ = selectedItem!.Macro.Should().BeSameAs(selectedOther);
    }

    [Fact]
    public void UpsertSelectedMacro_WhenSelectionExists_UpdatesCurrentItemWithoutAppendingDuplicate()
    {
        var original = CreateMacro("Original Macro");
        var updated = CreateMacro("Updated Macro");

        _viewModel.SetMacro(original);
        var originalItem = _viewModel.SelectedMacroItem;
        originalItem!.SequenceRepeatCount = 3;

        _viewModel.UpsertSelectedMacro(updated);

        _ = _viewModel.LoadedMacros.Should().ContainSingle();
        _ = _viewModel.SelectedMacroItem.Should().BeSameAs(originalItem);
        _ = _viewModel.SelectedMacroItem!.Macro.Should().BeSameAs(updated);
        _ = _viewModel.SelectedMacroItem.Name.Should().Be("Updated Macro");
        _ = _viewModel.SelectedMacroItem.SequenceRepeatCount.Should().Be(3);
        _ = _viewModel.CurrentMacro.Should().BeSameAs(updated);
    }

    [Fact]
    public async Task RemoveLoadedMacroCommand_WhenConfirmed_RemovesSelectedItemAndSelectsAdjacentItem()
    {
        _viewModel.SetMacro(CreateMacro("First Macro"));
        _viewModel.SetMacro(CreateMacro("Second Macro"));

        var firstItem = _viewModel.LoadedMacros[0];
        var secondItem = _viewModel.LoadedMacros[1];
        _viewModel.SelectedMacroItem = firstItem;
        _ = _dialogService.ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>(), "Yes", "No")
            .Returns(Task.FromResult(true));

        await _viewModel.RemoveLoadedMacroCommand.ExecuteAsync(firstItem);

        _ = _viewModel.LoadedMacros.Should().ContainSingle();
        _ = _viewModel.LoadedMacros[0].Should().BeSameAs(secondItem);
        _ = _viewModel.SelectedMacroItem.Should().BeSameAs(secondItem);
        _ = _viewModel.CurrentMacro.Should().BeSameAs(secondItem.Macro);
        _ = _viewModel.Status.Should().Be("[Files_StatusRemoved] First Macro");
    }

    [Fact]
    public async Task RemoveLoadedMacroCommand_WhenCancelled_DoesNotRemoveItem()
    {
        _viewModel.SetMacro(CreateMacro("Only Macro"));

        var item = _viewModel.SelectedMacroItem;
        _ = _dialogService.ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>(), "Yes", "No")
            .Returns(Task.FromResult(false));

        await _viewModel.RemoveLoadedMacroCommand.ExecuteAsync(item);

        _ = _viewModel.LoadedMacros.Should().ContainSingle();
        _ = _viewModel.SelectedMacroItem.Should().BeSameAs(item);
        _ = _viewModel.CurrentMacro.Should().BeSameAs(item!.Macro);
        _ = _viewModel.Status.Should().Be("[Files_StatusReady]");
    }

    [Fact]
    public async Task RemoveLoadedMacroCommand_WhenConfirmedAndLastItemRemoved_ResetsSelectionState()
    {
        _viewModel.SetMacro(CreateMacro("Only Macro"));

        var item = _viewModel.SelectedMacroItem;
        _ = _dialogService.ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>(), "Yes", "No")
            .Returns(Task.FromResult(true));

        await _viewModel.RemoveLoadedMacroCommand.ExecuteAsync(item);

        _ = _viewModel.LoadedMacros.Should().BeEmpty();
        _ = _viewModel.HasLoadedMacros.Should().BeFalse();
        _ = _viewModel.SelectedMacroItem.Should().BeNull();
        _ = _viewModel.CurrentMacro.Should().BeNull();
        _ = _viewModel.MacroName.Should().Be("New Macro");
        _ = _viewModel.HasRecordedMacro.Should().BeFalse();
        _ = _viewModel.Status.Should().Be("[Files_StatusRemoved] Only Macro");
    }

    [Fact]
    public void MacroName_WhenSelectedItemIsRenamed_RaisesPropertyChangedAndReturnsNormalizedValue()
    {
        _viewModel.SetMacro(CreateMacro("Rename Me"));
        var changedProperties = new List<string?>();
        _viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        _viewModel.MacroName = "   ";

        _ = _viewModel.MacroName.Should().Be("New Macro");
        _ = _viewModel.SelectedMacroItem!.Name.Should().Be("New Macro");
        _ = changedProperties.Should().Contain(nameof(FilesViewModel.MacroName));
    }

    [Fact]
    public void SelectedSequenceRepeatCount_UpdatesSelectedItemAndClampsMinimum()
    {
        _viewModel.SetMacro(CreateMacro("Sequence Macro"));

        _viewModel.SelectedSequenceRepeatCount = 5;

        _ = _viewModel.SelectedMacroItem!.SequenceRepeatCount.Should().Be(5);
        _ = _viewModel.SelectedSequenceRepeatCount.Should().Be(5);
        _ = _viewModel.SelectedMacroItem.SequenceRepeatSummary.Should().Contain("[Files_SequenceRepeatSummary]");
        _ = _viewModel.SelectedMacroItem.SequenceRepeatSummary.Should().Contain("5");

        _viewModel.SelectedSequenceRepeatCount = 0;

        _ = _viewModel.SelectedMacroItem.SequenceRepeatCount.Should().Be(1);
        _ = _viewModel.SelectedSequenceRepeatCount.Should().Be(1);
    }

    [Fact]
    public void SetMacro_WhenEventsCollectionIsNull_DoesNotThrowAndMarksAsNoRecordedMacro()
    {
        var macro = new MacroSequence { Name = "Corrupted" };

        Action act = () => _viewModel.SetMacro(macro);

        _ = act.Should().NotThrow();
        _ = _viewModel.CurrentMacro.Should().BeSameAs(macro);
        _ = _viewModel.HasRecordedMacro.Should().BeFalse();
    }

    [Fact]
    public void PlaybackModeProperties_UpdateSharedSessionMode()
    {
        _viewModel.IsAdvanceSelectionMode = true;

        _ = _loadedMacroSession.PlaybackMode.Should().Be(LoadedMacroPlaybackMode.AdvanceSelection);
        _ = _viewModel.IsAdvanceSelectionMode.Should().BeTrue();

        _viewModel.IsSequentialCycleMode = true;

        _ = _loadedMacroSession.PlaybackMode.Should().Be(LoadedMacroPlaybackMode.SequentialCycle);
        _ = _viewModel.IsSequentialCycleMode.Should().BeTrue();
        _ = _viewModel.IsSelectedOnlyMode.Should().BeFalse();
    }

    [Fact]
    public void ShowSequenceRepeatSettings_IsVisibleOnlyWhenSequentialCycleModeHasLoadedMacro()
    {
        _ = _viewModel.ShowSequenceRepeatSettings.Should().BeFalse();

        _viewModel.SetMacro(CreateMacro("Sequence Macro"));
        _ = _viewModel.ShowSequenceRepeatSettings.Should().BeFalse();

        _viewModel.IsSequentialCycleMode = true;
        _ = _viewModel.ShowSequenceRepeatSettings.Should().BeTrue();

        _viewModel.IsAdvanceSelectionMode = true;
        _ = _viewModel.ShowSequenceRepeatSettings.Should().BeFalse();
    }

    [Fact]
    public async Task SaveMacroAsync_WhenNoMacro_DoesNothing()
    {
        await _viewModel.SaveMacroAsync();

        _ = await _dialogService.DidNotReceive().ShowSaveFileDialogAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<FileDialogFilter[]>());
    }

    [Fact]
    public async Task SaveMacroAsync_WhenSelectionChangesDuringAwait_KeepsOriginalMacroNameAndSourcePath()
    {
        var firstMacro = CreateMacro("First Macro");
        var secondMacro = CreateMacro("Second Macro");
        _viewModel.SetMacro(firstMacro);
        var firstItem = _viewModel.SelectedMacroItem;
        _viewModel.MacroName = "Pinned First Macro";
        _viewModel.SetMacro(secondMacro);
        var secondItem = _viewModel.SelectedMacroItem;
        _viewModel.SelectedMacroItem = firstItem;

        var dialogCompletion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var saveCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        MacroSequence? savedMacro = null;
        string? savedPath = null;

        _ = _dialogService.ShowSaveFileDialogAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
            .Returns(_ => dialogCompletion.Task);
        _ = _fileManager.SaveAsync(Arg.Any<MacroSequence>(), Arg.Any<string>())
            .Returns(async callInfo =>
            {
                savedMacro = callInfo.ArgAt<MacroSequence>(0);
                savedPath = callInfo.ArgAt<string>(1);
                _ = await saveCompletion.Task;
            });

        var saveTask = _viewModel.SaveMacroAsync();
        _viewModel.SelectedMacroItem = secondItem;
        dialogCompletion.SetResult("/path/to/first.macro");
        await Task.Yield();
        saveCompletion.SetResult(true);
        await saveTask;

        _ = savedMacro.Should().NotBeSameAs(firstMacro);
        _ = savedMacro!.Name.Should().Be("Pinned First Macro");
        _ = firstMacro.Name.Should().Be("Pinned First Macro");
        _ = savedPath.Should().Be("/path/to/first.macro");
        _ = firstItem!.SourcePath.Should().Be("/path/to/first.macro");
        _ = secondItem!.SourcePath.Should().BeNull();
    }

    [Fact]
    public async Task SaveMacroAsync_WhenCancelled_UpdatesStatus()
    {
        _viewModel.SetMacro(CreateMacro());
        _ = _dialogService.ShowSaveFileDialogAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
            .Returns(Task.FromResult<string?>(null));

        await _viewModel.SaveMacroAsync();

        _ = _viewModel.Status.Should().Be("[Files_StatusSaveCancelled]");
        await _fileManager.DidNotReceive().SaveAsync(Arg.Any<MacroSequence>(), Arg.Any<string>());
    }

    [Fact]
    public async Task SaveMacroAsync_WhenSuccessful_SavesSelectedMacroAndUpdatesStatus()
    {
        var macro = CreateMacro();
        _viewModel.SetMacro(macro);
        _viewModel.MacroName = "MyMacro";

        _ = _dialogService.ShowSaveFileDialogAsync("[Files_SaveDialogTitle]", "MyMacro.macro", Arg.Any<FileDialogFilter[]>())
            .Returns(Task.FromResult<string?>("/path/to/MyMacro.macro"));

        await _viewModel.SaveMacroAsync();

        await _fileManager.Received(1).SaveAsync(
            Arg.Is<MacroSequence>(saved => !ReferenceEquals(saved, macro) && saved.Name == "MyMacro"),
            "/path/to/MyMacro.macro");
        _ = _viewModel.Status.Should().Contain("[Files_StatusSavedTo]");
        _ = _viewModel.Status.Should().Contain("MyMacro.macro");
        _ = macro.Name.Should().Be("MyMacro");
        _ = _viewModel.SelectedMacroItem!.SourcePath.Should().Be("/path/to/MyMacro.macro");
        _ = _viewModel.SelectedMacroItem.Description.Should().Contain("MyMacro.macro");
    }

    [Fact]
    public async Task SaveMacroAsync_WhenCurrentPositionEventHasStaleCoordinates_SavesNormalizedClone()
    {
        var macro = new MacroSequence
        {
            Name = "Stale Current Position",
            Events =
            {
                new MacroEvent
                {
                    Type = EventType.Click,
                    Button = MacroMouseButton.Left,
                    UseCurrentPosition = true,
                    X = 123,
                    Y = 456,
                    CoordinateMode = MouseCoordinateMode.Absolute,
                },
            },
        };
        _viewModel.SetMacro(macro);

        _ = _dialogService.ShowSaveFileDialogAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
            .Returns(Task.FromResult<string?>("/path/to/stale.macro"));

        MacroSequence? savedMacro = null;
        _ = _fileManager.SaveAsync(Arg.Do<MacroSequence>(saved => savedMacro = saved), "/path/to/stale.macro")
            .Returns(Task.CompletedTask);

        await _viewModel.SaveMacroAsync();

        _ = savedMacro.Should().NotBeNull();
        _ = savedMacro.Should().NotBeSameAs(macro);
        _ = savedMacro!.Events.Should().ContainSingle();
        _ = savedMacro.Events[0].X.Should().Be(0);
        _ = savedMacro.Events[0].Y.Should().Be(0);
        _ = savedMacro.Events[0].CoordinateMode.Should().BeNull();

        _ = macro.Events[0].X.Should().Be(123);
        _ = macro.Events[0].Y.Should().Be(456);
        _ = macro.Events[0].CoordinateMode.Should().Be(MouseCoordinateMode.Absolute);
    }

    [Fact]
    public async Task SaveMacroAsync_WhenOnlyAbsoluteEventIsCurrentPosition_RecomputesSnapshotAsNonAbsolute()
    {
        var macro = new MacroSequence
        {
            Name = "Current Position Only",
            IsAbsoluteCoordinates = true,
            Events =
            {
                new MacroEvent
                {
                    Type = EventType.Click,
                    Button = MacroMouseButton.Left,
                    UseCurrentPosition = true,
                    CoordinateMode = MouseCoordinateMode.Absolute,
                    X = 77,
                    Y = 88,
                },
            },
        };
        _viewModel.SetMacro(macro);
        _ = _dialogService.ShowSaveFileDialogAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
            .Returns("/tmp/current-position-only.macro");

        MacroSequence? saved = null;
        _ = _fileManager.SaveAsync(Arg.Do<MacroSequence>(m => saved = m), Arg.Any<string>())
            .Returns(Task.CompletedTask);

        await _viewModel.SaveMacroAsync();

        _ = saved.Should().NotBeNull();
        _ = saved!.IsAbsoluteCoordinates.Should().BeFalse();
        _ = saved.Events[0].CoordinateMode.Should().BeNull();
        _ = macro.IsAbsoluteCoordinates.Should().BeTrue();
    }

    [Fact]
    public async Task LoadMacroAsync_WhenCancelled_UpdatesStatus()
    {
        _ = _dialogService.ShowOpenFileDialogAsync(Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
            .Returns(Task.FromResult<string?>(null));

        await _viewModel.LoadMacroAsync();

        _ = _viewModel.Status.Should().Be("[Files_StatusLoadCancelled]");
        _ = await _fileManager.DidNotReceive().LoadAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task LoadMacroAsync_WhenSuccessful_LoadsIntoSessionAndUpdatesStatus()
    {
        var macro = CreateMacro("LoadedMacro");
        _ = _dialogService.ShowOpenFileDialogAsync(Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
            .Returns(Task.FromResult<string?>("/path/to/file.macro"));
        _ = _fileManager.LoadAsync("/path/to/file.macro").Returns(Task.FromResult<MacroSequence?>(macro));

        MacroSequence? loadedMacroFromEvent = null;
        _viewModel.MacroLoaded += (_, loadedMacro) => loadedMacroFromEvent = loadedMacro;

        await _viewModel.LoadMacroAsync();

        _ = _viewModel.CurrentMacro.Should().BeSameAs(macro);
        _ = _viewModel.MacroName.Should().Be("LoadedMacro");
        _ = _viewModel.HasRecordedMacro.Should().BeTrue();
        _ = _viewModel.SelectedSequenceRepeatCount.Should().Be(1);
        _ = _viewModel.Status.Should().Contain("[Files_StatusLoaded]");
        _ = _viewModel.Status.Should().Contain("file.macro");
        _ = _viewModel.LoadedMacros.Should().ContainSingle();
        _ = _viewModel.SelectedMacroItem!.SourcePath.Should().Be("/path/to/file.macro");
        _ = loadedMacroFromEvent.Should().BeSameAs(macro);
    }

    [Fact]
    public void CultureChanged_RefreshesLoadedMacroLocalizedProperties()
    {
        var localizationService = Substitute.For<ILocalizationService>();
        _ = localizationService.CurrentCulture.Returns(System.Globalization.CultureInfo.GetCultureInfo("en"));
        _ = localizationService["Files_StatusReady"].Returns("[Files_StatusReady]");
        _ = localizationService["Files_UnnamedMacro"].Returns("[Files_UnnamedMacro]");
        _ = localizationService["Files_SourceSession"].Returns("[Files_SourceSession]");
        _ = localizationService["Files_SequenceRepeatSummary"].Returns("[Files_SequenceRepeatSummary] {0}");
        _ = localizationService["Files_LoadedMacroDescription"].Returns("[Files_LoadedMacroDescription] {0} | {1}");
        _ = localizationService["Files_StatusLoadCancelled"].Returns("[Files_StatusLoadCancelled]");
        _ = localizationService["Files_StatusSaveCancelled"].Returns("[Files_StatusSaveCancelled]");
        var session = new LoadedMacroSession(localizationService);
        var viewModel = new FilesViewModel(_fileManager, _dialogService, session, localizationService);
        viewModel.SetMacro(CreateMacro(string.Empty));
        var item = viewModel.SelectedMacroItem!;
        var changedProperties = new List<string?>();
        item.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        _ = localizationService["Files_UnnamedMacro"].Returns("[Files_UnnamedMacro:tr]");
        _ = localizationService["Files_SourceSession"].Returns("[Files_SourceSession:tr]");
        _ = localizationService["Files_SequenceRepeatSummary"].Returns("[Files_SequenceRepeatSummary:tr] {0}");
        _ = localizationService["Files_LoadedMacroDescription"].Returns("[Files_LoadedMacroDescription:tr] {0} | {1}");

        localizationService.CultureChanged += Raise.Event<EventHandler>(localizationService, EventArgs.Empty);

        _ = changedProperties.Should().Contain(nameof(LoadedMacroListItem.SourceDescription));
        _ = changedProperties.Should().Contain(nameof(LoadedMacroListItem.SequenceRepeatSummary));
        _ = changedProperties.Should().Contain(nameof(LoadedMacroListItem.Description));
    }

    [Fact]
    public void CultureChanged_WhenReadyStatusDisplayed_RebuildsStatusInNewLanguage()
    {
        _ = _localizationService["Files_StatusReady"].Returns("[Files_StatusReady:tr]");

        _localizationService.CultureChanged += Raise.Event<EventHandler>(_localizationService, EventArgs.Empty);

        _ = _viewModel.Status.Should().Be("[Files_StatusReady:tr]");
    }

    [Fact]
    public async Task CultureChanged_WhenLoadCancelledStatusDisplayed_RebuildsStatusInNewLanguage()
    {
        _ = _dialogService.ShowOpenFileDialogAsync(Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
            .Returns(Task.FromResult<string?>(null));

        await _viewModel.LoadMacroAsync();

        _ = _localizationService["Files_StatusLoadCancelled"].Returns("[Files_StatusLoadCancelled:tr]");

        _localizationService.CultureChanged += Raise.Event<EventHandler>(_localizationService, EventArgs.Empty);

        _ = _viewModel.Status.Should().Be("[Files_StatusLoadCancelled:tr]");
    }

    [Fact]
    public async Task CultureChanged_WhenSaveCancelledStatusDisplayed_RebuildsStatusInNewLanguage()
    {
        _viewModel.SetMacro(CreateMacro());
        _ = _dialogService.ShowSaveFileDialogAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
            .Returns(Task.FromResult<string?>(null));

        await _viewModel.SaveMacroAsync();

        _ = _localizationService["Files_StatusSaveCancelled"].Returns("[Files_StatusSaveCancelled:tr]");

        _localizationService.CultureChanged += Raise.Event<EventHandler>(_localizationService, EventArgs.Empty);

        _ = _viewModel.Status.Should().Be("[Files_StatusSaveCancelled:tr]");
    }

    [Fact]
    public async Task SaveMacroAsync_WhenFileManagerThrows_UpdatesErrorStatus()
    {
        var macro = CreateMacro();
        _viewModel.SetMacro(macro);

        _ = _dialogService.ShowSaveFileDialogAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
            .Returns(Task.FromResult<string?>("/tmp/fail.macro"));
        _ = _fileManager.SaveAsync(Arg.Any<MacroSequence>(), Arg.Any<string>())
            .Returns(Task.FromException(new InvalidOperationException("write failed")));

        await _viewModel.SaveMacroAsync();

        _ = _viewModel.Status.Should().Contain("[Files_StatusSaveError]");
        _ = _viewModel.Status.Should().Contain("write failed");
    }

    [Fact]
    public async Task LoadMacroAsync_WhenFileManagerThrows_UpdatesErrorStatus()
    {
        _ = _dialogService.ShowOpenFileDialogAsync(Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
            .Returns(Task.FromResult<string?>("/tmp/fail.macro"));
        _ = _fileManager.LoadAsync("/tmp/fail.macro")
            .Returns(Task.FromException<MacroSequence?>(new InvalidOperationException("read failed")));

        await _viewModel.LoadMacroAsync();

        _ = _viewModel.Status.Should().Contain("[Files_StatusLoadError]");
        _ = _viewModel.Status.Should().Contain("read failed");
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
