using CrossMacro.Core.Models.Automation.Shortcuts;

namespace CrossMacro.Core.Services.Automation.Shortcuts;

public sealed class ShortcutStartingEventArgs(ShortcutTask task) : EventArgs
{
    public ShortcutTask Task { get; } = task ?? throw new ArgumentNullException(nameof(task));
}
