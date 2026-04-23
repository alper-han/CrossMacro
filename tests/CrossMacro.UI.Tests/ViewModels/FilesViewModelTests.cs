using System;
using System.Collections.Generic;
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

public class FilesViewModelTests
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
        _localizationService.CurrentCulture.Returns(System.Globalization.CultureInfo.GetCultureInfo("en"));
        _localizationService[Arg.Any<string>()].Returns(call => call.Arg<string>() switch
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
            _ => call.Arg<string>()
        });
        _loadedMacroSession = new LoadedMacroSession(_localizationService);
        _viewModel = new FilesViewModel(_fileManager, _dialogService, _loadedMacroSession, _localizationService);
    }

    [Fact]
    public void Construction_InitializesProperties()
    {
        _viewModel.Status.Should().Be("[Files_StatusReady]");
        _viewModel.MacroName.Should().Be("New Macro");
        _viewModel.SelectedSequenceRepeatCount.Should().Be(1);
        _viewModel.HasRecordedMacro.Should().BeFalse();
        _viewModel.HasLoadedMacros.Should().BeFalse();
        _viewModel.GetCurrentMacro().Should().BeNull();
        _viewModel.IsSelectedOnlyMode.Should().BeTrue();
        _viewModel.ShowSequenceRepeatSettings.Should().BeFalse();
    }

    [Fact]
    public void SetMacro_AddsMacroToSessionAndSelectsIt()
    {
        var macro = CreateMacro("Test Macro");

        _viewModel.SetMacro(macro);

        _viewModel.GetCurrentMacro().Should().BeSameAs(macro);
        _viewModel.HasRecordedMacro.Should().BeTrue();
        _viewModel.HasLoadedMacros.Should().BeTrue();
        _viewModel.MacroName.Should().Be("Test Macro");
        _viewModel.SelectedSequenceRepeatCount.Should().Be(1);
        _viewModel.LoadedMacros.Should().HaveCount(1);
        _viewModel.SelectedMacroItem.Should().NotBeNull();
        _viewModel.SelectedMacroItem!.Macro.Should().BeSameAs(macro);
    }

    [Fact]
    public void SetMacro_WhenRecorderUsesDefaultPlaceholder_AppliesCurrentMacroName()
    {
        _viewModel.MacroName = "Recorded Macro";
        var macro = CreateMacro("New Macro");

        _viewModel.SetMacro(macro);

        macro.Name.Should().Be("Recorded Macro");
        _viewModel.SelectedMacroItem!.Name.Should().Be("Recorded Macro");
        _viewModel.MacroName.Should().Be("Recorded Macro");
    }

    [Fact]
    public void SetMacro_WhenAnotherLoadedMacroIsSelected_DoesNotReuseSelectedNameForNewRecording()
    {
        _viewModel.SetMacro(CreateMacro("Existing Macro"));
        var recordedMacro = CreateMacro("New Macro");

        _viewModel.SetMacro(recordedMacro);

        _viewModel.LoadedMacros.Should().HaveCount(2);
        _viewModel.LoadedMacros[0].Name.Should().Be("Existing Macro");
        _viewModel.LoadedMacros[1].Name.Should().Be("New Macro");
        recordedMacro.Name.Should().Be("New Macro");
    }

    [Fact]
    public void SetMacro_AddsToSessionWithoutReplacingExistingLoadedMacros()
    {
        var firstMacro = CreateMacro("First Macro");
        var secondMacro = CreateMacro("Second Macro");

        _viewModel.SetMacro(firstMacro);
        _viewModel.SetMacro(secondMacro);

        _viewModel.LoadedMacros.Should().HaveCount(2);
        _viewModel.LoadedMacros[0].Macro.Should().BeSameAs(firstMacro);
        _viewModel.LoadedMacros[1].Macro.Should().BeSameAs(secondMacro);
        _viewModel.SelectedMacroItem.Should().BeSameAs(_viewModel.LoadedMacros[1]);
        _viewModel.GetCurrentMacro().Should().BeSameAs(secondMacro);
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

        result.Should().BeSameAs(trackedItem);
        _viewModel.LoadedMacros.Should().HaveCount(2);
        trackedItem.Macro.Should().BeSameAs(updatedTracked);
        trackedItem.Name.Should().Be("Tracked Updated");
        _viewModel.SelectedMacroItem.Should().BeSameAs(selectedItem);
        selectedItem!.Macro.Should().BeSameAs(selectedOther);
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

        _viewModel.LoadedMacros.Should().ContainSingle();
        _viewModel.SelectedMacroItem.Should().BeSameAs(originalItem);
        _viewModel.SelectedMacroItem!.Macro.Should().BeSameAs(updated);
        _viewModel.SelectedMacroItem.Name.Should().Be("Updated Macro");
        _viewModel.SelectedMacroItem.SequenceRepeatCount.Should().Be(3);
        _viewModel.GetCurrentMacro().Should().BeSameAs(updated);
    }

    [Fact]
    public async Task RemoveLoadedMacroCommand_WhenConfirmed_RemovesSelectedItemAndSelectsAdjacentItem()
    {
        _viewModel.SetMacro(CreateMacro("First Macro"));
        _viewModel.SetMacro(CreateMacro("Second Macro"));

        var firstItem = _viewModel.LoadedMacros[0];
        var secondItem = _viewModel.LoadedMacros[1];
        _viewModel.SelectedMacroItem = firstItem;
        _dialogService.ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>(), "Yes", "No")
            .Returns(Task.FromResult(true));

        await _viewModel.RemoveLoadedMacroCommand.ExecuteAsync(firstItem);

        _viewModel.LoadedMacros.Should().ContainSingle();
        _viewModel.LoadedMacros[0].Should().BeSameAs(secondItem);
        _viewModel.SelectedMacroItem.Should().BeSameAs(secondItem);
        _viewModel.GetCurrentMacro().Should().BeSameAs(secondItem.Macro);
        _viewModel.Status.Should().Be("[Files_StatusRemoved] First Macro");
    }

    [Fact]
    public async Task RemoveLoadedMacroCommand_WhenCancelled_DoesNotRemoveItem()
    {
        _viewModel.SetMacro(CreateMacro("Only Macro"));

        var item = _viewModel.SelectedMacroItem;
        _dialogService.ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>(), "Yes", "No")
            .Returns(Task.FromResult(false));

        await _viewModel.RemoveLoadedMacroCommand.ExecuteAsync(item);

        _viewModel.LoadedMacros.Should().ContainSingle();
        _viewModel.SelectedMacroItem.Should().BeSameAs(item);
        _viewModel.GetCurrentMacro().Should().BeSameAs(item!.Macro);
        _viewModel.Status.Should().Be("[Files_StatusReady]");
    }

    [Fact]
    public async Task RemoveLoadedMacroCommand_WhenConfirmedAndLastItemRemoved_ResetsSelectionState()
    {
        _viewModel.SetMacro(CreateMacro("Only Macro"));

        var item = _viewModel.SelectedMacroItem;
        _dialogService.ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>(), "Yes", "No")
            .Returns(Task.FromResult(true));

        await _viewModel.RemoveLoadedMacroCommand.ExecuteAsync(item);

        _viewModel.LoadedMacros.Should().BeEmpty();
        _viewModel.HasLoadedMacros.Should().BeFalse();
        _viewModel.SelectedMacroItem.Should().BeNull();
        _viewModel.GetCurrentMacro().Should().BeNull();
        _viewModel.MacroName.Should().Be("New Macro");
        _viewModel.HasRecordedMacro.Should().BeFalse();
        _viewModel.Status.Should().Be("[Files_StatusRemoved] Only Macro");
    }

    [Fact]
    public void MacroName_WhenSelectedItemIsRenamed_RaisesPropertyChangedAndReturnsNormalizedValue()
    {
        _viewModel.SetMacro(CreateMacro("Rename Me"));
        var changedProperties = new List<string?>();
        _viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        _viewModel.MacroName = "   ";

        _viewModel.MacroName.Should().Be("New Macro");
        _viewModel.SelectedMacroItem!.Name.Should().Be("New Macro");
        changedProperties.Should().Contain(nameof(FilesViewModel.MacroName));
    }

    [Fact]
    public void SelectedSequenceRepeatCount_UpdatesSelectedItemAndClampsMinimum()
    {
        _viewModel.SetMacro(CreateMacro("Sequence Macro"));

        _viewModel.SelectedSequenceRepeatCount = 5;

        _viewModel.SelectedMacroItem!.SequenceRepeatCount.Should().Be(5);
        _viewModel.SelectedSequenceRepeatCount.Should().Be(5);
        _viewModel.SelectedMacroItem.SequenceRepeatSummary.Should().Contain("[Files_SequenceRepeatSummary]");
        _viewModel.SelectedMacroItem.SequenceRepeatSummary.Should().Contain("5");

        _viewModel.SelectedSequenceRepeatCount = 0;

        _viewModel.SelectedMacroItem.SequenceRepeatCount.Should().Be(1);
        _viewModel.SelectedSequenceRepeatCount.Should().Be(1);
    }

    [Fact]
    public void SetMacro_WhenEventsCollectionIsNull_DoesNotThrowAndMarksAsNoRecordedMacro()
    {
        var macro = new MacroSequence { Name = "Corrupted", Events = null! };

        Action act = () => _viewModel.SetMacro(macro);

        act.Should().NotThrow();
        _viewModel.GetCurrentMacro().Should().BeSameAs(macro);
        _viewModel.HasRecordedMacro.Should().BeFalse();
    }

    [Fact]
    public void PlaybackModeProperties_UpdateSharedSessionMode()
    {
        _viewModel.IsAdvanceSelectionMode = true;

        _loadedMacroSession.PlaybackMode.Should().Be(LoadedMacroPlaybackMode.AdvanceSelection);
        _viewModel.IsAdvanceSelectionMode.Should().BeTrue();

        _viewModel.IsSequentialCycleMode = true;

        _loadedMacroSession.PlaybackMode.Should().Be(LoadedMacroPlaybackMode.SequentialCycle);
        _viewModel.IsSequentialCycleMode.Should().BeTrue();
        _viewModel.IsSelectedOnlyMode.Should().BeFalse();
    }

    [Fact]
    public void ShowSequenceRepeatSettings_IsVisibleOnlyWhenSequentialCycleModeHasLoadedMacro()
    {
        _viewModel.ShowSequenceRepeatSettings.Should().BeFalse();

        _viewModel.SetMacro(CreateMacro("Sequence Macro"));
        _viewModel.ShowSequenceRepeatSettings.Should().BeFalse();

        _viewModel.IsSequentialCycleMode = true;
        _viewModel.ShowSequenceRepeatSettings.Should().BeTrue();

        _viewModel.IsAdvanceSelectionMode = true;
        _viewModel.ShowSequenceRepeatSettings.Should().BeFalse();
    }

    [Fact]
    public async Task SaveMacroAsync_WhenNoMacro_DoesNothing()
    {
        await _viewModel.SaveMacroAsync();

        await _dialogService.DidNotReceive().ShowSaveFileDialogAsync(
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

        _dialogService.ShowSaveFileDialogAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
            .Returns(_ => dialogCompletion.Task);
        _fileManager.SaveAsync(Arg.Any<MacroSequence>(), Arg.Any<string>())
            .Returns(async callInfo =>
            {
                savedMacro = callInfo.ArgAt<MacroSequence>(0);
                savedPath = callInfo.ArgAt<string>(1);
                await saveCompletion.Task;
            });

        var saveTask = _viewModel.SaveMacroAsync();
        _viewModel.SelectedMacroItem = secondItem;
        dialogCompletion.SetResult("/path/to/first.macro");
        await Task.Yield();
        saveCompletion.SetResult(true);
        await saveTask;

        savedMacro.Should().BeSameAs(firstMacro);
        savedMacro!.Name.Should().Be("Pinned First Macro");
        savedPath.Should().Be("/path/to/first.macro");
        firstItem!.SourcePath.Should().Be("/path/to/first.macro");
        secondItem!.SourcePath.Should().BeNull();
    }

    [Fact]
    public async Task SaveMacroAsync_WhenCancelled_UpdatesStatus()
    {
        _viewModel.SetMacro(CreateMacro());
        _dialogService.ShowSaveFileDialogAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
            .Returns(Task.FromResult<string?>(null));

        await _viewModel.SaveMacroAsync();

        _viewModel.Status.Should().Be("[Files_StatusSaveCancelled]");
        await _fileManager.DidNotReceive().SaveAsync(Arg.Any<MacroSequence>(), Arg.Any<string>());
    }

    [Fact]
    public async Task SaveMacroAsync_WhenSuccessful_SavesSelectedMacroAndUpdatesStatus()
    {
        var macro = CreateMacro();
        _viewModel.SetMacro(macro);
        _viewModel.MacroName = "MyMacro";

        _dialogService.ShowSaveFileDialogAsync("[Files_SaveDialogTitle]", "MyMacro.macro", Arg.Any<FileDialogFilter[]>())
            .Returns(Task.FromResult<string?>("/path/to/MyMacro.macro"));

        await _viewModel.SaveMacroAsync();

        await _fileManager.Received(1).SaveAsync(macro, "/path/to/MyMacro.macro");
        _viewModel.Status.Should().Contain("[Files_StatusSavedTo]");
        _viewModel.Status.Should().Contain("MyMacro.macro");
        macro.Name.Should().Be("MyMacro");
        _viewModel.SelectedMacroItem!.SourcePath.Should().Be("/path/to/MyMacro.macro");
        _viewModel.SelectedMacroItem.Description.Should().Contain("MyMacro.macro");
    }

    [Fact]
    public async Task LoadMacroAsync_WhenCancelled_UpdatesStatus()
    {
        _dialogService.ShowOpenFileDialogAsync(Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
            .Returns(Task.FromResult<string?>(null));

        await _viewModel.LoadMacroAsync();

        _viewModel.Status.Should().Be("[Files_StatusLoadCancelled]");
        await _fileManager.DidNotReceive().LoadAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task LoadMacroAsync_WhenSuccessful_LoadsIntoSessionAndUpdatesStatus()
    {
        var macro = CreateMacro("LoadedMacro");
        _dialogService.ShowOpenFileDialogAsync(Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
            .Returns(Task.FromResult<string?>("/path/to/file.macro"));
        _fileManager.LoadAsync("/path/to/file.macro").Returns(Task.FromResult<MacroSequence?>(macro));

        MacroSequence? loadedMacroFromEvent = null;
        _viewModel.MacroLoaded += (_, loadedMacro) => loadedMacroFromEvent = loadedMacro;

        await _viewModel.LoadMacroAsync();

        _viewModel.GetCurrentMacro().Should().BeSameAs(macro);
        _viewModel.MacroName.Should().Be("LoadedMacro");
        _viewModel.HasRecordedMacro.Should().BeTrue();
        _viewModel.SelectedSequenceRepeatCount.Should().Be(1);
        _viewModel.Status.Should().Contain("[Files_StatusLoaded]");
        _viewModel.Status.Should().Contain("file.macro");
        _viewModel.LoadedMacros.Should().ContainSingle();
        _viewModel.SelectedMacroItem!.SourcePath.Should().Be("/path/to/file.macro");
        loadedMacroFromEvent.Should().BeSameAs(macro);
    }

    [Fact]
    public void CultureChanged_RefreshesLoadedMacroLocalizedProperties()
    {
        var localizationService = Substitute.For<ILocalizationService>();
        localizationService.CurrentCulture.Returns(System.Globalization.CultureInfo.GetCultureInfo("en"));
        localizationService["Files_StatusReady"].Returns("[Files_StatusReady]");
        localizationService["Files_UnnamedMacro"].Returns("[Files_UnnamedMacro]");
        localizationService["Files_SourceSession"].Returns("[Files_SourceSession]");
        localizationService["Files_SequenceRepeatSummary"].Returns("[Files_SequenceRepeatSummary] {0}");
        localizationService["Files_LoadedMacroDescription"].Returns("[Files_LoadedMacroDescription] {0} | {1}");
        localizationService["Files_StatusLoadCancelled"].Returns("[Files_StatusLoadCancelled]");
        localizationService["Files_StatusSaveCancelled"].Returns("[Files_StatusSaveCancelled]");
        var session = new LoadedMacroSession(localizationService);
        var viewModel = new FilesViewModel(_fileManager, _dialogService, session, localizationService);
        viewModel.SetMacro(CreateMacro(string.Empty));
        var item = viewModel.SelectedMacroItem!;
        var changedProperties = new List<string?>();
        item.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        localizationService["Files_UnnamedMacro"].Returns("[Files_UnnamedMacro:tr]");
        localizationService["Files_SourceSession"].Returns("[Files_SourceSession:tr]");
        localizationService["Files_SequenceRepeatSummary"].Returns("[Files_SequenceRepeatSummary:tr] {0}");
        localizationService["Files_LoadedMacroDescription"].Returns("[Files_LoadedMacroDescription:tr] {0} | {1}");

        localizationService.CultureChanged += Raise.Event<EventHandler>(localizationService, EventArgs.Empty);

        changedProperties.Should().Contain(nameof(LoadedMacroListItem.SourceDescription));
        changedProperties.Should().Contain(nameof(LoadedMacroListItem.SequenceRepeatSummary));
        changedProperties.Should().Contain(nameof(LoadedMacroListItem.Description));
    }

    [Fact]
    public void CultureChanged_WhenReadyStatusDisplayed_RebuildsStatusInNewLanguage()
    {
        _localizationService["Files_StatusReady"].Returns("[Files_StatusReady:tr]");

        _localizationService.CultureChanged += Raise.Event<EventHandler>(_localizationService, EventArgs.Empty);

        _viewModel.Status.Should().Be("[Files_StatusReady:tr]");
    }

    [Fact]
    public async Task CultureChanged_WhenLoadCancelledStatusDisplayed_RebuildsStatusInNewLanguage()
    {
        _dialogService.ShowOpenFileDialogAsync(Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
            .Returns(Task.FromResult<string?>(null));

        await _viewModel.LoadMacroAsync();

        _localizationService["Files_StatusLoadCancelled"].Returns("[Files_StatusLoadCancelled:tr]");

        _localizationService.CultureChanged += Raise.Event<EventHandler>(_localizationService, EventArgs.Empty);

        _viewModel.Status.Should().Be("[Files_StatusLoadCancelled:tr]");
    }

    [Fact]
    public async Task CultureChanged_WhenSaveCancelledStatusDisplayed_RebuildsStatusInNewLanguage()
    {
        _viewModel.SetMacro(CreateMacro());
        _dialogService.ShowSaveFileDialogAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
            .Returns(Task.FromResult<string?>(null));

        await _viewModel.SaveMacroAsync();

        _localizationService["Files_StatusSaveCancelled"].Returns("[Files_StatusSaveCancelled:tr]");

        _localizationService.CultureChanged += Raise.Event<EventHandler>(_localizationService, EventArgs.Empty);

        _viewModel.Status.Should().Be("[Files_StatusSaveCancelled:tr]");
    }

    [Fact]
    public async Task SaveMacroAsync_WhenFileManagerThrows_UpdatesErrorStatus()
    {
        var macro = CreateMacro();
        _viewModel.SetMacro(macro);

        _dialogService.ShowSaveFileDialogAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
            .Returns(Task.FromResult<string?>("/tmp/fail.macro"));
        _fileManager.SaveAsync(Arg.Any<MacroSequence>(), Arg.Any<string>())
            .Returns(Task.FromException(new InvalidOperationException("write failed")));

        await _viewModel.SaveMacroAsync();

        _viewModel.Status.Should().Contain("[Files_StatusSaveError]");
        _viewModel.Status.Should().Contain("write failed");
    }

    [Fact]
    public async Task LoadMacroAsync_WhenFileManagerThrows_UpdatesErrorStatus()
    {
        _dialogService.ShowOpenFileDialogAsync(Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
            .Returns(Task.FromResult<string?>("/tmp/fail.macro"));
        _fileManager.LoadAsync("/tmp/fail.macro")
            .Returns(Task.FromException<MacroSequence?>(new InvalidOperationException("read failed")));

        await _viewModel.LoadMacroAsync();

        _viewModel.Status.Should().Contain("[Files_StatusLoadError]");
        _viewModel.Status.Should().Contain("read failed");
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
