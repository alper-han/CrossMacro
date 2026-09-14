namespace CrossMacro.UI.Tests.ViewModels;

public sealed class AutomationTaskScopeTests
{
    [Theory]
    [InlineData("schedule")]
    [InlineData("shortcut")]
    [InlineData("trigger")]
    public async Task DeleteConfirmation_AcrossProfilesWithSameTaskId_DoesNotDeleteNewProfilesTask(string feature)
    {
        using var gate = new AutomationTaskMutationGate();
        var dialogs = Substitute.For<IDialogService>();
        var confirmation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = dialogs.ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(confirmation.Task);
        using var host = CreateHost(feature, gate, dialogs);
        await host.Initialize();
        var originalEditor = host.Editor();
        var delete = host.Delete(originalEditor);

        await ReplaceProfileAsync(host, gate);
        Assert.NotSame(originalEditor, host.Editor());
        confirmation.SetResult(true);
        await delete;

        Assert.Equal("Profile B task", host.Name(), StringComparer.Ordinal);
        Assert.Equal(0, host.SaveCount());
        await dialogs.Received().ShowMessageAsync(Arg.Any<string>(), Arg.Is<string>(text => text.Contains("active profile changed", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("schedule", false)]
    [InlineData("schedule", true)]
    [InlineData("shortcut", false)]
    [InlineData("shortcut", true)]
    [InlineData("trigger", false)]
    [InlineData("trigger", true)]
    public async Task OldEditor_AcrossProfilesWithSameTaskId_CannotSaveOrToggleNewProfilesTask(string feature, bool toggle)
    {
        using var gate = new AutomationTaskMutationGate();
        var dialogs = Substitute.For<IDialogService>();
        using var host = CreateHost(feature, gate, dialogs);
        await host.Initialize();
        var originalEditor = host.Editor();
        await ReplaceProfileAsync(host, gate);

        if (toggle) { await host.Toggle(originalEditor); }
        else { await host.Save(originalEditor); }

        Assert.Equal("Profile B task", host.Name(), StringComparer.Ordinal);
        Assert.Equal(0, host.SaveCount());
        await dialogs.Received().ShowMessageAsync(Arg.Any<string>(), Arg.Is<string>(text => text.Contains("active profile changed", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("schedule")]
    [InlineData("shortcut")]
    [InlineData("trigger")]
    public async Task OpenFilePicker_AcrossProfilesWithSameTaskId_DoesNotChangeNewEditor(string feature)
    {
        using var gate = new AutomationTaskMutationGate();
        var dialogs = Substitute.For<IDialogService>();
        var picker = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = dialogs.ShowOpenFileDialogAsync(Arg.Any<string>(), Arg.Any<FileDialogFilter[]>()).Returns(picker.Task);
        using var host = CreateHost(feature, gate, dialogs);
        await host.Initialize();
        var originalEditor = host.Editor();
        var browse = host.Browse();

        await ReplaceProfileAsync(host, gate);
        picker.SetResult("old-profile-choice.macro");
        await browse;

        Assert.NotSame(originalEditor, host.Editor());
        Assert.Equal("profile-b.macro", host.MacroPath(), StringComparer.Ordinal);
        Assert.Equal(0, host.SaveCount());
    }

    [Fact]
    public async Task LateOlderListResult_DoesNotReplaceNewerProfileProjection()
    {
        var manager = Substitute.For<IManageShortcut>();
        var initial = new TaskCompletionSource<TaskCollectionResult<ShortcutTask>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var old = new ShortcutTask { Name = "Old" };
        var current = new ShortcutTask { Id = old.Id, Name = "Current" };
        _ = manager.ListAsync(Arg.Any<CancellationToken>()).Returns(initial.Task, Task.FromResult(new TaskCollectionResult<ShortcutTask>([current], scopeGeneration: 1)));
        var runtime = Substitute.For<IShortcutService>();
        _ = runtime.Tasks.Returns(new ObservableCollection<ShortcutTask>());
        using var vm = new ShortcutViewModel(manager, runtime, Substitute.For<IDialogService>(),
            Substitute.For<IGlobalHotkeyService>(), CreateLocalization(), uiDispatcher: new SerializedUiDispatcher());
        var oldRefresh = vm.RefreshEditorsAsync(propagateError: true);
        await vm.RefreshEditorsAsync(propagateError: true);
        initial.SetResult(new TaskCollectionResult<ShortcutTask>([old], scopeGeneration: 0));
        await oldRefresh;

        Assert.Equal("Current", vm.Tasks.Single().Name, StringComparer.Ordinal);
        Assert.Equal(1, vm.Tasks.Single().ScopeGeneration);
    }

    [Theory]
    [InlineData("schedule")]
    [InlineData("shortcut")]
    [InlineData("trigger")]
    public async Task ClosingViewModel_BeforeDeleteConfirmation_DoesNotMutateTasks(string feature)
    {
        using var gate = new AutomationTaskMutationGate();
        var dialogs = Substitute.For<IDialogService>();
        var confirmation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = dialogs.ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(confirmation.Task);
        using var host = CreateHost(feature, gate, dialogs);
        await host.Initialize();
        var delete = host.Delete(host.Editor());
        host.Dispose();
        confirmation.SetResult(true);
        await delete;
        Assert.Equal(0, host.SaveCount());
    }

    [Theory]
    [InlineData("schedule")]
    [InlineData("shortcut")]
    [InlineData("trigger")]
    public async Task PreviousProfilesRuntimeEvents_DoNotChangeSameIdEditor_ButCurrentEventsStillUpdate(string feature)
    {
        using var gate = new AutomationTaskMutationGate();
        using var host = CreateHost(feature, gate, Substitute.For<IDialogService>());
        await host.Initialize();
        await ReplaceProfileAsync(host, gate);

        host.PublishOldCompletion();
        Assert.Null(host.LastStatus());
        host.PublishCurrentCompletion();
        Assert.Equal("current complete", host.LastStatus(), StringComparer.Ordinal);
    }

    [Fact]
    public async Task ClosingViewModel_BeforeListCompletes_DoesNotPublishProjection()
    {
        var manager = Substitute.For<IManageShortcut>();
        var completion = new TaskCompletionSource<TaskCollectionResult<ShortcutTask>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = manager.ListAsync(Arg.Any<CancellationToken>()).Returns(completion.Task);
        var runtime = Substitute.For<IShortcutService>();
        _ = runtime.Tasks.Returns(new ObservableCollection<ShortcutTask>());
        using var vm = new ShortcutViewModel(manager, runtime, Substitute.For<IDialogService>(),
            Substitute.For<IGlobalHotkeyService>(), CreateLocalization(), uiDispatcher: new SerializedUiDispatcher());
        var refresh = vm.RefreshEditorsAsync(propagateError: true);
        vm.Dispose();
        completion.SetResult(new TaskCollectionResult<ShortcutTask>([new ShortcutTask()], scopeGeneration: 1));
        await refresh;
        Assert.Empty(vm.Tasks);
    }

    private static async Task ReplaceProfileAsync(TaskUiHost host, AutomationTaskMutationGate gate)
    {
        await gate.RunAsync(() =>
        {
            _ = gate.AdvanceScopeGeneration();
            host.ReplaceProfile();
            return Task.CompletedTask;
        }, CancellationToken.None);
        await host.Refresh();
    }

    private static ILocalizationService CreateLocalization()
    {
        var localization = Substitute.For<ILocalizationService>();
        _ = localization.CurrentCulture.Returns(CultureInfo.InvariantCulture);
        _ = localization[Arg.Any<string>()].Returns(call => call.Arg<string>() + " {0}");
        return localization;
    }

    private static TaskUiHost CreateHost(string feature, AutomationTaskMutationGate gate, IDialogService dialogs)
    {
        var id = Guid.NewGuid();
        if (string.Equals(feature, "schedule", StringComparison.Ordinal))
        {
            var tasks = new ObservableCollection<ScheduledTask> { new() { Id = id, Name = "Profile A task", MacroFilePath = "profile-a.macro" } };
            var runtime = Substitute.For<ISchedulerService>();
            _ = runtime.Tasks.Returns(tasks);
            var originalTask = tasks.Single();
            var vm = new ScheduleViewModel(UiAutomationTestComposition.Create(runtime, gate), runtime, dialogs, TimeProvider.System, CreateLocalization(), uiDispatcher: new SerializedUiDispatcher());
            return new(vm.Dispose, vm.InitializeAsync, () => vm.RefreshEditorsAsync(propagateError: true), () => vm.Tasks.Single(),
                () => vm.Tasks.Single().Name, () => vm.Tasks.Single().MacroFilePath,
                () => { tasks.Clear(); tasks.Add(new ScheduledTask { Id = id, Name = "Profile B task", MacroFilePath = "profile-b.macro" }); },
                editor => vm.RemoveTaskCommand.ExecuteAsync((ScheduledTaskEditor)editor),
                editor => { vm.SelectedTask = (ScheduledTaskEditor)editor; vm.SelectedTask.Name = "stale edit"; return vm.SaveCommand.ExecuteAsync(parameter: null); },
                editor => { var task = (ScheduledTaskEditor)editor; task.IsEnabled = true; return vm.TaskEnabledChangedCommand.ExecuteAsync(task); },
                () => vm.BrowseMacroCommand.ExecuteAsync(parameter: null),
                () => runtime.ReceivedCalls().Count(call => string.Equals(call.GetMethodInfo().Name, nameof(ISchedulerService.SaveAsync), StringComparison.Ordinal)),
                () => { originalTask.LastStatus = "old complete"; runtime.TaskExecuted += Raise.Event<EventHandler<TaskExecutedEventArgs>>(runtime, new TaskExecutedEventArgs(originalTask, success: true)); },
                () => { var current = tasks.Single(); current.LastStatus = "current complete"; runtime.TaskExecuted += Raise.Event<EventHandler<TaskExecutedEventArgs>>(runtime, new TaskExecutedEventArgs(current, success: true)); },
                () => vm.Tasks.Single().LastStatus);
        }
        if (string.Equals(feature, "shortcut", StringComparison.Ordinal))
        {
            var tasks = new ObservableCollection<ShortcutTask> { new() { Id = id, Name = "Profile A task", MacroFilePath = "profile-a.macro" } };
            var runtime = Substitute.For<IShortcutService>();
            _ = runtime.Tasks.Returns(tasks);
            var originalTask = tasks.Single();
            var vm = new ShortcutViewModel(UiAutomationTestComposition.Create(runtime, gate), runtime, dialogs, Substitute.For<IGlobalHotkeyService>(), CreateLocalization(), uiDispatcher: new SerializedUiDispatcher());
            return new(vm.Dispose, vm.InitializeAsync, () => vm.RefreshEditorsAsync(propagateError: true), () => vm.Tasks.Single(),
                () => vm.Tasks.Single().Name, () => vm.Tasks.Single().MacroFilePath,
                () => { tasks.Clear(); tasks.Add(new ShortcutTask { Id = id, Name = "Profile B task", MacroFilePath = "profile-b.macro" }); },
                editor => vm.RemoveTaskCommand.ExecuteAsync((ShortcutTaskEditor)editor),
                editor => { vm.SelectedTask = (ShortcutTaskEditor)editor; vm.SelectedTask.Name = "stale edit"; return vm.SaveCommand.ExecuteAsync(parameter: null); },
                editor => { var task = (ShortcutTaskEditor)editor; task.IsEnabled = true; return vm.TaskEnabledChangedCommand.ExecuteAsync(task); },
                () => vm.BrowseMacroCommand.ExecuteAsync(parameter: null),
                () => runtime.ReceivedCalls().Count(call => string.Equals(call.GetMethodInfo().Name, nameof(IShortcutService.SaveAsync), StringComparison.Ordinal)),
                () => { originalTask.LastStatus = "old complete"; runtime.ShortcutExecuted += Raise.Event<EventHandler<ShortcutExecutedEventArgs>>(runtime, new ShortcutExecutedEventArgs(originalTask, success: true)); },
                () => { var current = tasks.Single(); current.LastStatus = "current complete"; runtime.ShortcutExecuted += Raise.Event<EventHandler<ShortcutExecutedEventArgs>>(runtime, new ShortcutExecutedEventArgs(current, success: true)); },
                () => vm.Tasks.Single().LastStatus);
        }
        var triggerTasks = new ObservableCollection<TriggerTask> { new() { Id = id, Name = "Profile A task", MacroFilePath = "profile-a.macro" } };
        var triggerRuntime = Substitute.For<ITriggerService>();
        _ = triggerRuntime.Tasks.Returns(triggerTasks);
        var triggerOriginalTask = triggerTasks.Single();
        var triggerVm = new TriggerViewModel(UiAutomationTestComposition.Create(triggerRuntime, gate), triggerRuntime, profileManager: null, dialogs,
            CreateLocalization(), windowManager: null, uiDispatcher: new SerializedUiDispatcher());
        return new(triggerVm.Dispose, triggerVm.InitializeAsync, () => triggerVm.RefreshEditorsAsync(propagateError: true), () => triggerVm.Tasks.Single(),
            () => triggerVm.Tasks.Single().Name, () => triggerVm.Tasks.Single().MacroFilePath,
            () => { triggerTasks.Clear(); triggerTasks.Add(new TriggerTask { Id = id, Name = "Profile B task", MacroFilePath = "profile-b.macro" }); },
            editor => triggerVm.RemoveTaskCommand.ExecuteAsync((TriggerTaskEditor)editor),
            editor => { triggerVm.SelectedTask = (TriggerTaskEditor)editor; triggerVm.SelectedTask.Name = "stale edit"; return triggerVm.SaveCommand.ExecuteAsync(parameter: null); },
            editor => { var task = (TriggerTaskEditor)editor; task.IsEnabled = true; return triggerVm.TaskEnabledChangedCommand.ExecuteAsync(task); },
            () => triggerVm.BrowseMacroCommand.ExecuteAsync(parameter: null),
            () => triggerRuntime.ReceivedCalls().Count(call => string.Equals(call.GetMethodInfo().Name, nameof(ITriggerService.SaveAsync), StringComparison.Ordinal)),
            () => { triggerOriginalTask.LastStatus = "old complete"; triggerRuntime.TriggerFired += Raise.Event<EventHandler<TriggerFiredEventArgs>>(triggerRuntime, new TriggerFiredEventArgs(triggerOriginalTask, success: true)); },
            () => { var current = triggerTasks.Single(); current.LastStatus = "current complete"; triggerRuntime.TriggerFired += Raise.Event<EventHandler<TriggerFiredEventArgs>>(triggerRuntime, new TriggerFiredEventArgs(current, success: true)); },
            () => triggerVm.Tasks.Single().LastStatus);
    }

    private sealed record TaskUiHost(Action Close, Func<Task> Initialize, Func<Task> Refresh, Func<object> Editor,
        Func<string> Name, Func<string> MacroPath, Action ReplaceProfile, Func<object, Task> Delete,
        Func<object, Task> Save, Func<object, Task> Toggle, Func<Task> Browse, Func<int> SaveCount,
        Action PublishOldCompletion, Action PublishCurrentCompletion, Func<string?> LastStatus) : IDisposable
    {
        public void Dispose() => Close();
    }
}
