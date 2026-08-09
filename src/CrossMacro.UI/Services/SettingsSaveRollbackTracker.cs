namespace CrossMacro.UI.Services;

internal sealed class SettingsSaveRollbackTracker
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Action?> _rollbackActions = new(StringComparer.Ordinal);
    private Task? _latestSaveTask;

    public void Track(Task saveTask, Action rollback, IReadOnlyCollection<string> propertyNames)
    {
        ArgumentNullException.ThrowIfNull(saveTask);
        ArgumentNullException.ThrowIfNull(rollback);
        ArgumentNullException.ThrowIfNull(propertyNames);

        lock (_gate)
        {
            if (!ReferenceEquals(_latestSaveTask, saveTask))
            {
                _rollbackActions.Clear();
                _latestSaveTask = saveTask;
            }

            var key = CreateKey(propertyNames);
            if (!_rollbackActions.ContainsKey(key))
            {
                _rollbackActions[key] = rollback;
            }
        }
    }

    public bool TryTakeRollback(
        Task? saveTask,
        IReadOnlyCollection<string> propertyNames,
        out Action? rollback,
        out bool isTracked)
    {
        ArgumentNullException.ThrowIfNull(propertyNames);

        lock (_gate)
        {
            if (saveTask is null || !ReferenceEquals(_latestSaveTask, saveTask))
            {
                rollback = null;
                isTracked = false;
                return false;
            }

            var key = CreateKey(propertyNames);
            if (!_rollbackActions.TryGetValue(key, out rollback))
            {
                rollback = null;
                isTracked = false;
                return false;
            }

            isTracked = true;
            if (rollback is null)
            {
                return false;
            }

            _rollbackActions[key] = null;
            return true;
        }
    }

    private static string CreateKey(IEnumerable<string> propertyNames) =>
        string.Join('\u001F', propertyNames.Order(StringComparer.Ordinal));
}
