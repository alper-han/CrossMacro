namespace CrossMacro.Core.Services;

public sealed class ShortcutStartingEventArgs : EventArgs
{
    public ShortcutStartingEventArgs(ShortcutTask task)
    {
        Task = task ?? throw new ArgumentNullException(nameof(task));
    }

    public ShortcutTask Task { get; }
}
