using System;
using System.Collections.ObjectModel;
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

public class ScheduleViewModelTests
{
    private readonly ISchedulerService _schedulerService;
    private readonly IDialogService _dialogService;
    private readonly ScheduleViewModel _viewModel;

    public ScheduleViewModelTests()
    {
        _schedulerService = Substitute.For<ISchedulerService>();
        _dialogService = Substitute.For<IDialogService>();
        
        // Setup initial tasks list
        _schedulerService.Tasks.Returns(new ObservableCollection<ScheduledTask>());
        _schedulerService.LoadAsync().Returns(Task.CompletedTask);
        _schedulerService.SaveAsync().Returns(Task.CompletedTask);

        _viewModel = new ScheduleViewModel(_schedulerService, _dialogService);
    }

    [Fact]
    public async Task InitializeAsync_LoadsAndStartsService()
    {
        await _viewModel.InitializeAsync();

        await _schedulerService.Received(1).LoadAsync();
        _schedulerService.Received(1).Start();
    }

    [Fact]
    public async Task InitializeAsync_WhenCalledMultipleTimes_LoadsOnlyOnce()
    {
        await _viewModel.InitializeAsync();
        await _viewModel.InitializeAsync();

        await _schedulerService.Received(1).LoadAsync();
        _schedulerService.Received(1).Start();
    }

    [Fact]
    public async Task InitializeAsync_WhenLoadFails_ReportsStatusAndSkipsStart()
    {
        _schedulerService.LoadAsync().Returns(Task.FromException(new InvalidOperationException("load failed")));
        var statusTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _viewModel.StatusChanged += (_, status) => statusTcs.TrySetResult(status);

        await _viewModel.InitializeAsync();
        var reportedStatus = await statusTcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

        reportedStatus.Should().Contain("failed to initialize");
        reportedStatus.Should().Contain("load failed");
        _schedulerService.DidNotReceive().Start();
    }

    [Fact]
    public void AddTask_CreatesAndSelectsTask()
    {
        // Act
        _viewModel.AddTaskCommand.Execute(null);

        // Assert
        _schedulerService.Received(1).AddTask(Arg.Any<ScheduledTask>());
        _viewModel.SelectedTask.Should().NotBeNull();
        _viewModel.SelectedTask!.Name.Should().Contain("Task");
    }

    [Fact]
    public async Task RemoveTask_WhenConfirmed_RemovesTask()
    {
        // Arrange
        var task = new ScheduledTask();
        _schedulerService.Tasks.Add(task);
        _dialogService.ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(true));

        // Act
        await _viewModel.RemoveTaskCommand.ExecuteAsync(task);

        // Assert
        _schedulerService.Received(1).RemoveTask(task.Id);
        _ = _schedulerService.Received(1).SaveAsync();
    }

    [Fact]
    public async Task RemoveTask_WhenSaveFails_ReportsStatusAndShowsMessage()
    {
        // Arrange
        var task = new ScheduledTask();
        _schedulerService.Tasks.Add(task);
        _dialogService.ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(true));
        _schedulerService.SaveAsync().Returns(Task.FromException(new InvalidOperationException("disk full")));

        var statusTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _viewModel.StatusChanged += (_, status) =>
        {
            if (status.Contains("failed to save changes", StringComparison.OrdinalIgnoreCase))
            {
                statusTcs.TrySetResult(status);
            }
        };

        // Act
        await _viewModel.RemoveTaskCommand.ExecuteAsync(task);
        var status = await statusTcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        status.Should().Contain("failed to save changes");
        status.Should().Contain("disk full");
        await _dialogService.Received(1).ShowMessageAsync(
            "Schedule Save Failed",
            Arg.Is<string>(s => s.Contains("disk full")),
            Arg.Any<string>());
    }

    [Fact]
    public async Task RemoveTask_WhenCancelled_DoesNotRemove()
    {
        // Arrange
        var task = new ScheduledTask();
        _schedulerService.Tasks.Add(task);
        _dialogService.ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(false));

        // Act
        await _viewModel.RemoveTaskCommand.ExecuteAsync(task);

        // Assert
        _schedulerService.DidNotReceive().RemoveTask(Arg.Any<System.Guid>());
    }

    [Fact]
    public void ScheduleTypeSelection_UpdatesTaskType()
    {
        // Arrange
        var task = new ScheduledTask { Type = ScheduleType.Interval };
        _viewModel.SelectedTask = task;

        // Act
        _viewModel.IsDateTimeSelected = true;

        // Assert
        task.Type.Should().Be(ScheduleType.SpecificTime);
        _viewModel.IsIntervalSelected.Should().BeFalse();

        // Act 2
        _viewModel.IsIntervalSelected = true;

        // Assert 2
        task.Type.Should().Be(ScheduleType.Interval);
        _viewModel.IsDateTimeSelected.Should().BeFalse();
    }

    [Fact]
    public async Task BrowseMacro_UpdatesTaskPath()
    {
        // Arrange
        var task = new ScheduledTask();
        _viewModel.SelectedTask = task;
        _dialogService.ShowOpenFileDialogAsync(Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
            .Returns(Task.FromResult<string?>("test.macro"));

        // Act
        await _viewModel.BrowseMacroCommand.ExecuteAsync(null);

        // Assert
        task.MacroFilePath.Should().Be("test.macro");
    }

    [Fact]
    public async Task BrowseMacro_WhenCancelled_KeepsExistingPath()
    {
        // Arrange
        var task = new ScheduledTask { MacroFilePath = "existing.macro" };
        _viewModel.SelectedTask = task;
        _dialogService.ShowOpenFileDialogAsync(Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
            .Returns(Task.FromResult<string?>(null));

        // Act
        await _viewModel.BrowseMacroCommand.ExecuteAsync(null);

        // Assert
        task.MacroFilePath.Should().Be("existing.macro");
    }

    [Fact]
    public void SelectTask_WhenSameTaskSelected_TogglesToNull()
    {
        // Arrange
        var task = new ScheduledTask();
        _viewModel.SelectedTask = task;

        // Act
        _viewModel.SelectTaskCommand.Execute(task);

        // Assert
        _viewModel.SelectedTask.Should().BeNull();
    }

    [Fact]
    public void OnTaskEnabledChanged_WhenFileExtensionNotMacro_EmitsStatusWarning()
    {
        // Arrange
        var task = new ScheduledTask
        {
            MacroFilePath = "/tmp/sample.txt",
            IsEnabled = true
        };
        string? status = null;
        _viewModel.StatusChanged += (_, s) => status = s;

        // Act
        _viewModel.OnTaskEnabledChanged(task);

        // Assert
        status.Should().Contain(".macro");
        _schedulerService.Received(1).SetTaskEnabled(task.Id, true);
    }

    [Fact]
    public void ScheduledDateAndTime_WhenChanged_UpdatesSelectedTaskDateTime()
    {
        // Arrange
        var task = new ScheduledTask { ScheduledDateTime = new DateTime(2026, 1, 10, 8, 30, 0) };
        _viewModel.SelectedTask = task;

        // Act
        _viewModel.ScheduledDate = new DateTimeOffset(new DateTime(2026, 2, 15));
        _viewModel.ScheduledTime = new TimeSpan(14, 45, 20);

        // Assert
        task.ScheduledDateTime.Should().NotBeNull();
        task.ScheduledDateTime!.Value.Year.Should().Be(2026);
        task.ScheduledDateTime.Value.Month.Should().Be(2);
        task.ScheduledDateTime.Value.Day.Should().Be(15);
        task.ScheduledDateTime.Value.Hour.Should().Be(14);
        task.ScheduledDateTime.Value.Minute.Should().Be(45);
        task.ScheduledDateTime.Value.Second.Should().Be(20);
    }
}
