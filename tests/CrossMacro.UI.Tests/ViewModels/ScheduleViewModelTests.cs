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
    private readonly ILocalizationService _localizationService;
    private readonly ScheduleViewModel _viewModel;

    public ScheduleViewModelTests()
    {
        _schedulerService = Substitute.For<ISchedulerService>();
        _dialogService = Substitute.For<IDialogService>();
        _localizationService = Substitute.For<ILocalizationService>();
        _localizationService.CurrentCulture.Returns(System.Globalization.CultureInfo.GetCultureInfo("en"));
        _localizationService[Arg.Any<string>()].Returns(call => call.Arg<string>() switch
        {
            "Schedule_ItemsText" => "[Schedule_ItemsText] {0}",
            "Schedule_NoFileSelected" => "[Schedule_NoFileSelected]",
            "Schedule_StatusInitFailed" => "[Schedule_StatusInitFailed] {0}",
            "Schedule_DefaultTaskName" => "[Schedule_DefaultTaskName] {0}",
            "Schedule_DeleteTitle" => "[Schedule_DeleteTitle]",
            "Schedule_DeleteMessage" => "[Schedule_DeleteMessage] {0}",
            "Schedule_StatusSaveFailed" => "[Schedule_StatusSaveFailed] {0}",
            "Schedule_SaveFailedTitle" => "[Schedule_SaveFailedTitle]",
            "Schedule_OpenMacroDialogFilter" => "[Schedule_OpenMacroDialogFilter]",
            "Schedule_OpenMacroDialogTitle" => "[Schedule_OpenMacroDialogTitle]",
            "Schedule_StatusExtensionWarning" => "[Schedule_StatusExtensionWarning]",
            "Schedule_StatusRunning" => "[Schedule_StatusRunning] {0}",
            "Schedule_StatusCompleted" => "[Schedule_StatusCompleted] {0}",
            "Schedule_StatusFailedExecution" => "[Schedule_StatusFailedExecution] {0} | {1}",
            "Schedule_WeeklyEveryDay" => "[Schedule_WeeklyEveryDay]",
            "Schedule_WeeklyWeekdays" => "[Schedule_WeeklyWeekdays]",
            "Schedule_WeeklyWeekends" => "[Schedule_WeeklyWeekends]",
            "Schedule_WeeklyCustom" => "[Schedule_WeeklyCustom]",
            _ => call.Arg<string>()
        });
        var timeProvider = Substitute.For<ITimeProvider>();
        timeProvider.Now.Returns(new DateTime(2026, 1, 1, 10, 0, 0));
        timeProvider.UtcNow.Returns(new DateTime(2026, 1, 1, 7, 0, 0));
        
        // Setup initial tasks list
        _schedulerService.Tasks.Returns(new ObservableCollection<ScheduledTask>());
        _schedulerService.LoadAsync().Returns(Task.CompletedTask);
        _schedulerService.SaveAsync().Returns(Task.CompletedTask);

        _viewModel = new ScheduleViewModel(_schedulerService, _dialogService, timeProvider, _localizationService);
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

        reportedStatus.Should().Contain("[Schedule_StatusInitFailed]");
        reportedStatus.Should().Contain("load failed");
        _schedulerService.DidNotReceive().Start();
    }

    [Fact]
    public void CultureChanged_RaisesLocalizedComputedProperties()
    {
        var localizationService = Substitute.For<ILocalizationService>();
        localizationService.CurrentCulture.Returns(System.Globalization.CultureInfo.GetCultureInfo("en"));
        localizationService["Schedule_ItemsText"].Returns("{0} items");
        localizationService["Schedule_NoFileSelected"].Returns("No file selected");
        var timeProvider = Substitute.For<ITimeProvider>();
        timeProvider.Now.Returns(new DateTime(2026, 1, 1, 10, 0, 0));
        timeProvider.UtcNow.Returns(new DateTime(2026, 1, 1, 7, 0, 0));
        var viewModel = new ScheduleViewModel(_schedulerService, _dialogService, timeProvider, localizationService);
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        localizationService.CultureChanged += Raise.Event<EventHandler>(localizationService, EventArgs.Empty);

        changedProperties.Should().Contain(nameof(ScheduleViewModel.TaskCountText));
        changedProperties.Should().Contain(nameof(ScheduleViewModel.SelectedMacroFileName));
        changedProperties.Should().Contain(nameof(ScheduleViewModel.SelectedTask));
        changedProperties.Should().Contain(nameof(ScheduleViewModel.Tasks));
    }

    [Fact]
    public void AddTask_CreatesAndSelectsTask()
    {
        // Act
        _viewModel.AddTaskCommand.Execute(null);

        // Assert
        _schedulerService.Received(1).AddTask(Arg.Any<ScheduledTask>());
        _viewModel.SelectedTask.Should().NotBeNull();
        _viewModel.SelectedTask!.Name.Should().Contain("[Schedule_DefaultTaskName]");
    }

    [Fact]
    public void SelectedRunTexts_DisplayUtcRuntimeValuesAsLocalTime()
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        _localizationService.CurrentCulture.Returns(culture);
        var task = new ScheduledTask
        {
            LastRunTime = new DateTime(2026, 1, 1, 7, 0, 0, DateTimeKind.Utc),
            NextRunTime = new DateTime(2026, 1, 1, 8, 30, 0, DateTimeKind.Utc)
        };
        _viewModel.SelectedTask = task;

        _viewModel.SelectedLastRunText.Should().Be(task.LastRunTime.Value.ToLocalTime().ToString("G", culture));
        _viewModel.SelectedNextRunText.Should().Be(task.NextRunTime.Value.ToLocalTime().ToString("G", culture));
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
    public async Task RemoveTask_WhenSelectedTaskIsRemoved_SelectsFirstRemainingTask()
    {
        var first = new ScheduledTask { Name = "First" };
        var second = new ScheduledTask { Name = "Second" };
        _schedulerService.Tasks.Add(first);
        _schedulerService.Tasks.Add(second);
        _viewModel.SelectedTask = second;
        _dialogService.ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(true));

        await _viewModel.RemoveTaskCommand.ExecuteAsync(second);

        _viewModel.SelectedTask.Should().Be(first);
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
            if (status.Contains("disk full", StringComparison.OrdinalIgnoreCase))
            {
                statusTcs.TrySetResult(status);
            }
        };

        // Act
        await _viewModel.RemoveTaskCommand.ExecuteAsync(task);
        var status = await statusTcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

        // Assert
        status.Should().Contain("[Schedule_StatusSaveFailed]");
        status.Should().Contain("disk full");
        await _dialogService.Received(1).ShowMessageAsync(
            "[Schedule_SaveFailedTitle]",
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

        // Act 3
        _viewModel.IsWeeklySelected = true;

        // Assert 3
        task.Type.Should().Be(ScheduleType.Weekly);
        _viewModel.IsIntervalSelected.Should().BeFalse();
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
    public void SelectTask_WhenSameTaskSelected_KeepsSelection()
    {
        // Arrange
        var task = new ScheduledTask();
        _viewModel.SelectedTask = task;

        // Act
        _viewModel.SelectTaskCommand.Execute(task);

        // Assert
        _viewModel.SelectedTask.Should().Be(task);
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
        status.Should().Contain("[Schedule_StatusExtensionWarning]");
        _schedulerService.Received(1).SetTaskEnabled(task.Id, true);
    }

    [Fact]
    public async Task TaskEnabledChangedCommand_WhenToggleChanges_PersistsTasks()
    {
        // Arrange
        var task = new ScheduledTask
        {
            MacroFilePath = "/tmp/sample.macro",
            IsEnabled = true
        };

        // Act
        await _viewModel.TaskEnabledChangedCommand.ExecuteAsync(task);

        // Assert
        _schedulerService.Received(1).SetTaskEnabled(task.Id, true);
        await _schedulerService.Received(1).SaveAsync();
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

    [Fact]
    public void ScheduledDateAndTime_WhenInitialValueMissing_UsesInjectedTimeProviderNow()
    {
        // Arrange
        var timeProvider = Substitute.For<ITimeProvider>();
        timeProvider.Now.Returns(new DateTime(2032, 3, 4, 9, 10, 11));
        var viewModel = new ScheduleViewModel(_schedulerService, _dialogService, timeProvider, _localizationService);
        var task = new ScheduledTask();
        viewModel.SelectedTask = task;

        // Act
        viewModel.ScheduledDate = new DateTimeOffset(new DateTime(2032, 6, 7));
        viewModel.ScheduledTime = new TimeSpan(15, 20, 25);

        // Assert
        task.ScheduledDateTime.Should().Be(new DateTime(2032, 6, 7, 15, 20, 25));
    }

    [Fact]
    public void WeeklyTime_WhenChanged_UpdatesSelectedTaskTime()
    {
        var task = new ScheduledTask { Type = ScheduleType.Weekly, WeeklyTime = new TimeSpan(9, 0, 0) };
        _viewModel.SelectedTask = task;

        _viewModel.WeeklyTime = new TimeSpan(14, 45, 0);

        task.WeeklyTime.Should().Be(new TimeSpan(14, 45, 0));
    }

    [Fact]
    public void SelectedWeeklyPreset_WhenChanged_UpdatesWeeklyDays()
    {
        var task = new ScheduledTask { Type = ScheduleType.Weekly, WeeklyDays = ScheduleDays.Weekdays };
        _viewModel.SelectedTask = task;

        _viewModel.SelectedWeeklyPreset = _viewModel.WeeklyPresetOptions.Single(x => x.Value == ScheduleDays.Weekends);

        task.WeeklyDays.Should().Be(ScheduleDays.Weekends);
        _viewModel.IsWeeklyCustomSelected.Should().BeFalse();
    }

    [Fact]
    public void SelectedWeeklyPreset_WhenCustomSelected_ShowsCustomDaySelection()
    {
        var task = new ScheduledTask { Type = ScheduleType.Weekly, WeeklyDays = ScheduleDays.Weekdays };
        _viewModel.SelectedTask = task;

        _viewModel.SelectedWeeklyPreset = _viewModel.WeeklyPresetOptions.Single(x => x.Value == null);

        task.WeeklyDays.Should().Be(ScheduleDays.Weekdays);
        _viewModel.IsWeeklyCustomSelected.Should().BeTrue();
    }

    [Fact]
    public void WeeklyDaySelection_WhenChanged_UpdatesWeeklyDays()
    {
        var task = new ScheduledTask { Type = ScheduleType.Weekly, WeeklyDays = ScheduleDays.Monday };
        _viewModel.SelectedTask = task;

        var dayOptions = _viewModel.WeeklyDayOptions.ToArray();
        dayOptions.Single(option => option.Value == ScheduleDays.Wednesday).IsSelected = true;
        dayOptions.Single(option => option.Value == ScheduleDays.Monday).IsSelected = false;

        task.WeeklyDays.Should().Be(ScheduleDays.Wednesday);
        _viewModel.IsWeeklyCustomSelected.Should().BeTrue();
        dayOptions.Select(option => option.Value)
            .Should().Contain([ScheduleDays.Monday, ScheduleDays.Tuesday, ScheduleDays.Wednesday, ScheduleDays.Thursday, ScheduleDays.Friday, ScheduleDays.Saturday, ScheduleDays.Sunday]);
    }

    [Fact]
    public void SelectedTask_WhenWeeklyHasNoSelectedDays_CannotBeEnabled()
    {
        var task = new ScheduledTask
        {
            MacroFilePath = "test.macro",
            Type = ScheduleType.Weekly,
            WeeklyDays = ScheduleDays.None
        };

        _viewModel.SelectedTask = task;

        task.CanBeEnabled.Should().BeFalse();
    }

    [Fact]
    public void WeeklyDaySelection_WhenLastDayIsCleared_DisablesEnabledTask()
    {
        var task = new ScheduledTask
        {
            MacroFilePath = "test.macro",
            Type = ScheduleType.Weekly,
            WeeklyDays = ScheduleDays.Monday,
            WeeklyTime = new TimeSpan(9, 0, 0)
        };
        task.IsEnabled = true;
        _viewModel.SelectedTask = task;

        _viewModel.WeeklyDayOptions.Single(option => option.Value == ScheduleDays.Monday).IsSelected = false;

        task.IsEnabled.Should().BeFalse();
        task.CanBeEnabled.Should().BeFalse();
        task.NextRunTime.Should().BeNull();
    }
}
