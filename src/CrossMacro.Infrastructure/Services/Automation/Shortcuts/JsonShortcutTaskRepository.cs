namespace CrossMacro.Infrastructure.Services.Automation.Shortcuts;

/// <summary>Owns the shortcut file path and atomic JSON persistence.</summary>
public sealed class JsonShortcutTaskRepository(string filePath, IShortcutHotkeyNormalizer hotkeyNormalizer) : IShortcutTaskRepository
{
    private readonly IShortcutHotkeyNormalizer _hotkeyNormalizer = hotkeyNormalizer ?? throw new ArgumentNullException(nameof(hotkeyNormalizer));
    private string _filePath = ValidateFilePath(filePath);

    public async Task<IReadOnlyList<ShortcutTask>?> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(_filePath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(_filePath, cancellationToken).ConfigureAwait(false);
        var tasks = JsonSerializer.Deserialize(json, CrossMacroJsonContext.Default.ListShortcutTask);
        if (tasks is null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind is JsonValueKind.Array)
        {
            for (var index = 0; index < tasks.Count && index < document.RootElement.GetArrayLength(); index++)
            {
                var taskElement = document.RootElement[index];
                if (tasks[index].Hotkeys.Count is 0
                    && taskElement.TryGetProperty("hotkeyString", out var legacyHotkey)
                    && legacyHotkey.ValueKind is JsonValueKind.String)
                {
                    tasks[index].Hotkeys.Add(legacyHotkey.GetString() ?? string.Empty);
                }

                NormalizeTask(tasks[index]);
            }
        }
        else
        {
            foreach (var task in tasks)
            {
                NormalizeTask(task);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return tasks;
    }

    public Task SaveAsync(IReadOnlyList<ShortcutTask> tasks, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var task in tasks)
        {
            NormalizeTask(task);
        }

        return FileBackedJsonStorage.WriteAsync(_filePath, tasks.ToList(), CrossMacroJsonContext.Default.ListShortcutTask, cancellationToken);
    }

    public void SetProfileDirectory(string profileConfigDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileConfigDirectory);
        _filePath = Path.Combine(profileConfigDirectory, ConfigFileNames.Shortcuts);
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

    private static string ValidateFilePath(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return filePath;
    }
}
