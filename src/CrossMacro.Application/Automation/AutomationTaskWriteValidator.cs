namespace CrossMacro.Application.Automation;

/// <summary>
/// Applies the persisted-task invariants at the application write boundary.
/// Mutable task models remain serializer and binding friendly; this class makes
/// every staged commit pass through the same normalization policy.
/// </summary>
internal static class AutomationTaskWriteValidator
{
    internal static void NormalizeForCommit(IEnumerable<ScheduledTask> tasks)
    {
        ArgumentNullException.ThrowIfNull(tasks);

        foreach (var task in tasks)
        {
            task.Normalize();
        }
    }

    internal static void NormalizeForCommit(IEnumerable<ShortcutTask> tasks)
    {
        ArgumentNullException.ThrowIfNull(tasks);

        foreach (var task in tasks)
        {
            task.Normalize();
        }
    }

    internal static void NormalizeForCommit(IEnumerable<TriggerTask> tasks)
    {
        ArgumentNullException.ThrowIfNull(tasks);

        foreach (var task in tasks)
        {
            if (task.IsEnabled && !task.CanBeEnabled)
            {
                task.IsEnabled = false;
            }
        }
    }
}
