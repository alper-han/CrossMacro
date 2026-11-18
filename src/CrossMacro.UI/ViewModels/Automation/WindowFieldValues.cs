using CrossMacro.Platform.Abstractions.WindowManagement;

namespace CrossMacro.UI.ViewModels.Automation;

/// <summary>Suggests match values for a window rule from the windows that are currently open.</summary>
internal static class WindowFieldValues
{
    /// <summary>Must be awaited on the UI thread: the caller updates bound collections afterwards.</summary>
    internal static async Task<IReadOnlyList<string>> ListAsync(IWindowManager windowManager, TriggerField field)
    {
        var windows = await windowManager.GetWindowsAsync(CancellationToken.None).ConfigureAwait(true);
        IEnumerable<string> values = field switch
        {
            TriggerField.WindowClass => windows.Select(window => window.Class),
            TriggerField.WindowTitle => windows.Select(window => window.Title),
            TriggerField.Workspace => windows.Select(window => window.Workspace),
            TriggerField.ProcessName => windows.Select(window => window.ProcessName),
            TriggerField.None => [],
            _ => throw new InvalidOperationException("Unsupported window rule field."),
        };

        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
