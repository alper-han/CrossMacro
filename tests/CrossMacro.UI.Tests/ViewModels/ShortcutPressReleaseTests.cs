
namespace CrossMacro.UI.Tests.ViewModels;

public sealed class ShortcutPressReleaseTests
{
    [Fact]
    public void ModeTransitions_ExcludeLoopAndHeld_AndRestoreOrdinarySingleRun()
    {
        var editor = new ShortcutTaskEditor { MacroFilePath = "same.macro", ReleaseMacroFilePath = "same.macro" };
        editor.AddHotkey("F9");
        editor.IsLoopEnabled = true;
        editor.RunOnPressAndRelease = true;
        Assert.False(editor.IsLoopEnabled);
        Assert.False(editor.RunWhileHeld);
        Assert.True(editor.CanBeEnabled);
        editor.IsEnabled = true;
        editor.ReleaseMacroFilePath = "";
        Assert.False(editor.IsEnabled);
        editor.RunWhileHeld = true;
        Assert.False(editor.RunOnPressAndRelease);
        Assert.True(editor.IsLoopEnabled);
        editor.IsPressToStartStop = true;
        Assert.False(editor.RunWhileHeld);
        Assert.True(editor.IsPressToStartStop);
        Assert.False(editor.IsLoopEnabled);
        Assert.True(editor.CanBeEnabled);
    }

    [Fact]
    public async Task ReleasePicker_SameMacroCanBeSaved_AndRollbackRestoresBothPaths()
    {
        var macroPath = Path.GetTempFileName();
        try
        {
            var runtime = Substitute.For<IShortcutService>();
            var source = new ShortcutTask { MacroFilePath = macroPath, Hotkeys = { "F9" } };
            runtime.Tasks.Returns(new ObservableCollection<ShortcutTask> { source });
            var dialogs = Substitute.For<IDialogService>();
            dialogs.ShowOpenFileDialogAsync(Arg.Any<string>(), Arg.Any<FileDialogFilter[]>()).Returns(macroPath);
            using var vm = Create(runtime, dialogs);
            await vm.InitializeAsync();
            vm.SelectedTask!.RunOnPressAndRelease = true;
            await vm.BrowseReleaseMacroCommand.ExecuteAsync(parameter: null);
            Assert.True(vm.SelectedTask.CanBeEnabled);
            await vm.SaveCommand.ExecuteAsync(parameter: null);
            Assert.True(source.RunOnPressAndRelease);
            Assert.Equal(macroPath, source.ReleaseMacroFilePath, StringComparer.Ordinal);
            vm.SelectedTask.ReleaseMacroFilePath = "unsaved.macro";
            vm.SelectedTask.RunOnPressAndRelease = false;
            vm.SelectedTask.Rollback();
            Assert.True(vm.SelectedTask.RunOnPressAndRelease);
            Assert.Equal(macroPath, vm.SelectedTask.ReleaseMacroFilePath, StringComparer.Ordinal);
        }
        finally
        {
            File.Delete(macroPath);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReleasePicker_LateResultAfterTaskOrProfileSwitch_IsDiscarded(bool profileSwitch)
    {
        using var gate = new AutomationTaskMutationGate();
        var runtime = Substitute.For<IShortcutService>();
        var id = Guid.NewGuid();
        var tasks = new ObservableCollection<ShortcutTask> { new() { Id = id, MacroFilePath = "press.macro", ReleaseMacroFilePath = "original.macro", RunOnPressAndRelease = true, Hotkeys = { "F9" } } };
        runtime.Tasks.Returns(tasks);
        var dialogs = Substitute.For<IDialogService>();
        var picker = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        dialogs.ShowOpenFileDialogAsync(Arg.Any<string>(), Arg.Any<FileDialogFilter[]>()).Returns(picker.Task);
        using var vm = Create(runtime, dialogs, gate);
        await vm.InitializeAsync();
        var original = vm.SelectedTask!;
        var browse = vm.BrowseReleaseMacroCommand.ExecuteAsync(parameter: null);
        if (profileSwitch)
        {
            await gate.RunAsync(() =>
            {
                _ = gate.AdvanceScopeGeneration();
                tasks.Clear();
                tasks.Add(new ShortcutTask { Id = id, MacroFilePath = "new-press.macro", ReleaseMacroFilePath = "new-release.macro", RunOnPressAndRelease = true, Hotkeys = { "F9" } });
                return Task.CompletedTask;
            }, CancellationToken.None);
            await vm.RefreshEditorsAsync(propagateError: true);
        }
        else
        {
            vm.SelectedTask = new ShortcutTaskEditor { ReleaseMacroFilePath = "new-release.macro" };
        }
        picker.SetResult("stale-choice.macro");
        await browse;
        Assert.Equal("original.macro", original.ReleaseMacroFilePath, StringComparer.Ordinal);
        Assert.Equal("new-release.macro", vm.SelectedTask!.ReleaseMacroFilePath, StringComparer.Ordinal);
    }

    [Fact]
    public async Task ReleasePicker_Cancelled_KeepsPathAndFilename()
    {
        var runtime = Substitute.For<IShortcutService>();
        runtime.Tasks.Returns(new ObservableCollection<ShortcutTask>());
        var dialogs = Substitute.For<IDialogService>();
        dialogs.ShowOpenFileDialogAsync(Arg.Any<string>(), Arg.Any<FileDialogFilter[]>()).Returns(Task.FromResult<string?>(null));
        using var vm = Create(runtime, dialogs);
        vm.SelectedTask = new ShortcutTaskEditor { RunOnPressAndRelease = true, ReleaseMacroFilePath = "/tmp/release.macro" };
        await vm.BrowseReleaseMacroCommand.ExecuteAsync(parameter: null);
        Assert.Equal("/tmp/release.macro", vm.SelectedTask.ReleaseMacroFilePath, StringComparer.Ordinal);
        Assert.Equal("release.macro", vm.SelectedReleaseMacroFileName, StringComparer.Ordinal);
    }


    private static ShortcutViewModel Create(IShortcutService runtime, IDialogService dialogs, AutomationTaskMutationGate? gate = null)
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.CurrentCulture.Returns(System.Globalization.CultureInfo.GetCultureInfo("en"));
        localization[Arg.Any<string>()].Returns(call => call.Arg<string>());
        return new ShortcutViewModel(UiAutomationTestComposition.Create(runtime, gate), runtime, dialogs, Substitute.For<IGlobalHotkeyService>(), localization,
            new CrossMacro.Tests.AcceptingShortcutHotkeyNormalizer(), uiDispatcher: ImmediateUiDispatcher.Instance);
    }
}
