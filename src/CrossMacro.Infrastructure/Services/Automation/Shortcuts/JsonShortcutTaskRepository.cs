namespace CrossMacro.Infrastructure.Services.Automation.Shortcuts;

/// <summary>Owns the shortcut file path and atomic JSON persistence.</summary>
public sealed class JsonShortcutTaskRepository(string filePath, IShortcutHotkeyNormalizer hotkeyNormalizer) : IShortcutTaskRepository
{
    private readonly IShortcutHotkeyNormalizer _hotkeyNormalizer = hotkeyNormalizer ?? throw new ArgumentNullException(nameof(hotkeyNormalizer));
    private readonly JsonTaskFile<ShortcutTask> _file = new(
        filePath,
        ConfigFileNames.Shortcuts,
        CrossMacroJsonContext.Default.ListShortcutTask,
        ParseWithLegacyHotkeys);

    public async Task<IReadOnlyList<ShortcutTask>?> LoadAsync(CancellationToken cancellationToken = default)
    {
        var tasks = await _file.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (tasks is null)
        {
            return null;
        }

        foreach (var task in tasks)
        {
            NormalizeTask(task);
        }

        return tasks;
    }

    public Task SaveAsync(IReadOnlyList<ShortcutTask> tasks, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        foreach (var task in tasks)
        {
            NormalizeTask(task);
        }

        return _file.SaveAsync(tasks, cancellationToken);
    }

    public void SetProfileDirectory(string profileConfigDirectory) => _file.SetProfileDirectory(profileConfigDirectory);

    /// <summary>
    /// Reads shortcuts files written before a task carried a <c>hotkeys</c> list: those stored a single
    /// <c>hotkeyString</c> property, which is promoted to the first hotkey when the list is empty.
    /// </summary>
    private static List<ShortcutTask>? ParseWithLegacyHotkeys(string json)
    {
        var tasks = JsonSerializer.Deserialize(json, CrossMacroJsonContext.Default.ListShortcutTask);
        if (tasks is null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        for (var index = 0; index < tasks.Count; index++)
        {
            if (tasks[index].Hotkeys.Count is 0
                && root.ValueKind is JsonValueKind.Array
                && index < root.GetArrayLength()
                && root[index].TryGetProperty("hotkeyString", out var legacyHotkey)
                && legacyHotkey.ValueKind is JsonValueKind.String)
            {
                tasks[index].Hotkeys.Add(legacyHotkey.GetString() ?? string.Empty);
            }
        }

        return tasks;
    }

    private void NormalizeTask(ShortcutTask task)
    {
        var normalizedHotkeys = new List<string>(task.Hotkeys.Count);
        foreach (var hotkey in task.Hotkeys)
        {
            if (string.IsNullOrWhiteSpace(hotkey))
            {
                continue;
            }

            var normalized = NormalizeHotkey(hotkey);
            if (!normalizedHotkeys.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                normalizedHotkeys.Add(normalized);
            }
        }

        task.Hotkeys.Clear();
        foreach (var normalizedHotkey in normalizedHotkeys)
        {
            task.Hotkeys.Add(normalizedHotkey);
        }
        task.Normalize();
    }

    private string NormalizeHotkey(string hotkey)
    {
        if (!_hotkeyNormalizer.TryNormalize(hotkey, out var normalized, out var error) || normalized is null)
        {
            throw new JsonException(error ?? $"Invalid shortcut hotkey: {hotkey}.");
        }

        return normalized;
    }
}
