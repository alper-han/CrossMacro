namespace CrossMacro.Core.Services;

public sealed class ShortcutStartingEventArgs(ShortcutTask task) : EventArgs
{
    public ShortcutTask Task { get; } = task ?? throw new ArgumentNullException(nameof(task));
}
