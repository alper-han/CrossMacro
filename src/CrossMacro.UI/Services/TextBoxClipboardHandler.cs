namespace CrossMacro.UI.Services;

internal static class TextBoxClipboardHandler
{
    internal static async Task<bool> TryCopyAsync(
        TextBox textBox,
        Func<string, Task> setTextAsync)
    {
        ArgumentNullException.ThrowIfNull(textBox);
        ArgumentNullException.ThrowIfNull(setTextAsync);

        try
        {
            var selectedText = textBox.SelectedText;
            if (string.IsNullOrEmpty(selectedText))
            {
                return false;
            }

            await setTextAsync(selectedText).ConfigureAwait(true);
            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "[TextBoxClipboard] Failed to copy selected text; ignoring clipboard backend failure");
            return false;
        }
    }

    internal static async Task<bool> TryCutAsync(
        TextBox textBox,
        Func<string, Task> setTextAsync)
    {
        ArgumentNullException.ThrowIfNull(textBox);
        ArgumentNullException.ThrowIfNull(setTextAsync);

        try
        {
            var selectedText = textBox.SelectedText;
            if (string.IsNullOrEmpty(selectedText))
            {
                return false;
            }

            await setTextAsync(selectedText).ConfigureAwait(true);
            textBox.SelectedText = string.Empty;
            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "[TextBoxClipboard] Failed to cut selected text; keeping the text unchanged");
            return false;
        }
    }
}
