namespace CrossMacro.Application.Automation;

public sealed class ShortcutTaskKind : AutomationTaskKind<ShortcutTask>
{
    public static ShortcutTaskKind Instance { get; } = new();

    private ShortcutTaskKind()
    {
    }

    public override string Name => "shortcut";

    public override string CannotEnableReason => "Shortcut task requires a macro path, hotkeys, and valid window rules before it can be enabled.";

    public override Guid IdOf(ShortcutTask task) => task.Id;

    public override string NameOf(ShortcutTask task) => task.Name;

    public override ShortcutTask Copy(ShortcutTask task) => AutomationTaskSnapshots.Copy(task);

    public override void Normalize(ShortcutTask task) => task.Normalize();

    public override bool IsEnabled(ShortcutTask task) => task.IsEnabled;

    public override bool CanBeEnabled(ShortcutTask task) => task.CanBeEnabled;

    public override bool TrySetEnabled(ShortcutTask task, bool enabled) => task.TrySetEnabled(enabled);

    public override IEnumerable<string?> MacroPaths(ShortcutTask task)
    {
        yield return task.MacroFilePath;
        if (task.RunOnPressAndRelease)
        {
            yield return task.ReleaseMacroFilePath;
        }
    }

    public override void ValidateFiles(ShortcutTask task)
    {
        if (task.RunOnPressAndRelease && (!File.Exists(task.MacroFilePath) || !File.Exists(task.ReleaseMacroFilePath)))
        {
            throw new FileNotFoundException("Press/release mode requires available press and release macro files.");
        }
    }
}
