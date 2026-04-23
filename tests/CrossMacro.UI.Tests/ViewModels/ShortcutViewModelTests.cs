using System.Collections.ObjectModel;
using System;
using System.Linq;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.UI.Services;
using CrossMacro.UI.ViewModels;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CrossMacro.UI.Tests.ViewModels;

public class ShortcutViewModelTests
{
    private readonly IShortcutService _shortcutService;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;
    private readonly ShortcutViewModel _viewModel;

    public ShortcutViewModelTests()
    {
        _shortcutService = Substitute.For<IShortcutService>();
        _dialogService = Substitute.For<IDialogService>();
        _localizationService = Substitute.For<ILocalizationService>();
        _localizationService.CurrentCulture.Returns(System.Globalization.CultureInfo.GetCultureInfo("en"));
        _localizationService[Arg.Any<string>()].Returns(call => call.Arg<string>() switch
        {
            "Shortcut_ItemsText" => "[Shortcut_ItemsText] {0}",
            "Shortcut_NoFileSelected" => "[Shortcut_NoFileSelected]",
            "Shortcut_StatusInitFailed" => "[Shortcut_StatusInitFailed] {0}",
            "Shortcut_DefaultTaskName" => "[Shortcut_DefaultTaskName] {0}",
            "Shortcut_DeleteTitle" => "[Shortcut_DeleteTitle]",
            "Shortcut_DeleteMessage" => "[Shortcut_DeleteMessage] {0}",
            "Shortcut_StatusSaveFailed" => "[Shortcut_StatusSaveFailed] {0}",
            "Shortcut_SaveFailedTitle" => "[Shortcut_SaveFailedTitle]",
            "Shortcut_OpenMacroDialogFilter" => "[Shortcut_OpenMacroDialogFilter]",
            "Shortcut_OpenMacroDialogTitle" => "[Shortcut_OpenMacroDialogTitle]",
            "Shortcut_StatusRunning" => "[Shortcut_StatusRunning] {0}",
            "Shortcut_StatusCompleted" => "[Shortcut_StatusCompleted] {0}",
            "Shortcut_StatusFailed" => "[Shortcut_StatusFailed] {0} | {1}",
            "Shortcut_StatusChangesSaved" => "[Shortcut_StatusChangesSaved]",
            _ => call.Arg<string>()
        });
        
        _shortcutService.Tasks.Returns(new ObservableCollection<ShortcutTask>());

        _viewModel = new ShortcutViewModel(_shortcutService, _dialogService, _localizationService);
    }

    [Fact]
    public async Task Construction_LoadsAndStartsService()
    {
        for (int i = 0; i < 20; i++)
        {
            try
            {
                await _shortcutService.Received(1).LoadAsync();
                _shortcutService.Received(1).Start();
                return;
            }
            catch
            {
                await Task.Delay(25);
            }
        }

        await _shortcutService.Received(1).LoadAsync();
        _shortcutService.Received(1).Start();
    }

    [Fact]
    public async Task Construction_WhenLoadFails_ReportsStatusAndDoesNotThrow()
    {
        // Arrange
        var failingShortcutService = Substitute.For<IShortcutService>();
        failingShortcutService.Tasks.Returns(new ObservableCollection<ShortcutTask>());

        var loadTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        failingShortcutService.LoadAsync().Returns(_ => loadTcs.Task);

        var statusTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Act
        var vm = new ShortcutViewModel(failingShortcutService, _dialogService, _localizationService);
        vm.StatusChanged += (_, status) => statusTcs.TrySetResult(status);
        loadTcs.TrySetException(new InvalidOperationException("load failed"));
        var statusMessage = await statusTcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        statusMessage.Should().Contain("[Shortcut_StatusInitFailed]");
        statusMessage.Should().Contain("load failed");
        failingShortcutService.DidNotReceive().Start();
    }

    [Fact]
    public void CultureChanged_RaisesLocalizedComputedProperties()
    {
        var localizationService = Substitute.For<ILocalizationService>();
        localizationService.CurrentCulture.Returns(System.Globalization.CultureInfo.GetCultureInfo("en"));
        localizationService["Shortcut_ItemsText"].Returns("{0} items");
        localizationService["Shortcut_NoFileSelected"].Returns("No file selected");
        var vm = new ShortcutViewModel(_shortcutService, _dialogService, localizationService);
        var changedProperties = new List<string?>();
        vm.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        localizationService.CultureChanged += Raise.Event<EventHandler>(localizationService, EventArgs.Empty);

        changedProperties.Should().Contain(nameof(ShortcutViewModel.TaskCountText));
        changedProperties.Should().Contain(nameof(ShortcutViewModel.SelectedMacroFileName));
        changedProperties.Should().Contain(nameof(ShortcutViewModel.SelectedTask));
    }

    [Fact]
    public void AddTask_CreatesAndSelectsTask()
    {
        // Act
        _viewModel.AddTaskCommand.Execute(null);

        // Assert
        _shortcutService.Received(1).AddTask(Arg.Any<ShortcutTask>());
        _viewModel.SelectedTask.Should().NotBeNull();
        _viewModel.SelectedTask!.Name.Should().Contain("[Shortcut_DefaultTaskName]");
    }

    [Fact]
    public async Task RemoveTask_WhenConfirmed_RemovesTask()
    {
        // Arrange
        var task = new ShortcutTask();
        _shortcutService.Tasks.Add(task);
        _dialogService.ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(true));

        // Act
        await _viewModel.RemoveTaskCommand.ExecuteAsync(task);

        // Assert
        _shortcutService.Received(1).RemoveTask(task.Id);
        _ = _shortcutService.Received(1).SaveAsync();
    }

    [Fact]
    public async Task RemoveTask_WhenSaveFails_ReportsStatusAndShowsMessage()
    {
        // Arrange
        var task = new ShortcutTask();
        _shortcutService.Tasks.Add(task);
        _dialogService.ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(true));
        _shortcutService.SaveAsync().Returns(Task.FromException(new InvalidOperationException("disk full")));

        var statusTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _viewModel.StatusChanged += (_, status) =>
        {
            if (status.Contains("disk full", StringComparison.OrdinalIgnoreCase))
            {
                statusTcs.TrySetResult(status);
            }
        };

        // Act
        await _viewModel.RemoveTaskCommand.ExecuteAsync(task);
        var status = await statusTcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        status.Should().Contain("[Shortcut_StatusSaveFailed]");
        status.Should().Contain("disk full");
        await _dialogService.Received(1).ShowMessageAsync(
            "[Shortcut_SaveFailedTitle]",
            Arg.Is<string>(s => s.Contains("disk full")),
            Arg.Any<string>());
    }

    [Fact]
    public async Task RemoveTask_WhenCancelled_DoesNotRemove()
    {
        // Arrange
        var task = new ShortcutTask();
        _shortcutService.Tasks.Add(task);
        _dialogService.ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(false));

        // Act
        await _viewModel.RemoveTaskCommand.ExecuteAsync(task);

        // Assert
        _shortcutService.DidNotReceive().RemoveTask(Arg.Any<System.Guid>());
    }

    [Fact]
    public void OnHotkeyChanged_UpdatesSelectedTask()
    {
        // Arrange
        var task = new ShortcutTask();
        _viewModel.SelectedTask = task;

        // Act
        _viewModel.OnHotkeyChanged("F9");

        // Assert
        task.HotkeyString.Should().Be("F9");
        _viewModel.SelectedHotkeyString.Should().Be("F9");
    }

    [Fact]
    public async Task BrowseMacro_WhenCancelled_KeepsCurrentPath()
    {
        // Arrange
        var task = new ShortcutTask { MacroFilePath = "existing.macro" };
        _viewModel.SelectedTask = task;
        _dialogService.ShowOpenFileDialogAsync(Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
            .Returns(Task.FromResult<string?>(null));

        // Act
        await _viewModel.BrowseMacroCommand.ExecuteAsync(null);

        // Assert
        task.MacroFilePath.Should().Be("existing.macro");
    }

    [Fact]
    public void SelectTask_WhenSameTaskSelected_TogglesSelectionOff()
    {
        // Arrange
        var task = new ShortcutTask();
        _viewModel.SelectedTask = task;

        // Act
        _viewModel.SelectTaskCommand.Execute(task);

        // Assert
        _viewModel.SelectedTask.Should().BeNull();
    }

    [Fact]
    public async Task SaveCommand_InvokesShortcutServiceSave()
    {
        // Act
        await _viewModel.SaveCommand.ExecuteAsync(null);

        // Assert
        await _shortcutService.Received(1).SaveAsync();
    }
}
