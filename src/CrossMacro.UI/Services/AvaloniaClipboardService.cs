
namespace CrossMacro.UI.Services;

public class AvaloniaClipboardService : IClipboardService
{
    private readonly IDesktopLifetimeContext _desktopLifetimeContext;

    public AvaloniaClipboardService(IDesktopLifetimeContext desktopLifetimeContext)
    {
        _desktopLifetimeContext = desktopLifetimeContext;
    }

    public virtual bool IsSupported => _desktopLifetimeContext.MainWindow?.Clipboard is not null;

    public virtual async Task SetTextAsync(string text, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Log.Debug("[AvaloniaClipboard] SetTextAsync called for length {Length}", text.Length);

        if (_desktopLifetimeContext.MainWindow is null)
        {
            Log.Warning("[AvaloniaClipboard] SetTextAsync skipped because desktop main window is unavailable");
            return;
        }

        await (await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            Log.Debug("[AvaloniaClipboard] SetTextAsync running on UI thread");
            var clipboard = GetClipboard();
            if (clipboard is not null)
            {
                try
                {
                    Log.Debug("[AvaloniaClipboard] Setting text to clipboard instance: {Type}", clipboard.GetType().Name);
                    await ClipboardExtensions.SetTextAsync(clipboard, text).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    Log.Debug("[AvaloniaClipboard] SetTextAsync completed successfully");
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Log.LogError(ex, "[AvaloniaClipboard] Exception during SetTextAsync");
                    throw;
                }
            }
            else
            {
                Log.Warning("[AvaloniaClipboard] SetTextAsync: Clipboard is null");
            }
        }, DispatcherPriority.Normal, cancellationToken)).ConfigureAwait(false);
    }

    public virtual async Task<string?> GetTextAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Log.Debug("[AvaloniaClipboard] GetTextAsync called");

        if (_desktopLifetimeContext.MainWindow is null)
        {
            Log.Warning("[AvaloniaClipboard] GetTextAsync skipped because desktop main window is unavailable");
            return null;
        }

        return await (await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var clipboard = GetClipboard();
                if (clipboard is not null)
                {
                    var text = await ClipboardExtensions.TryGetTextAsync(clipboard).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    return text;
                }
                return null;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.LogError(ex, "Failed to get clipboard text via Avalonia");
                throw;
            }
        }, DispatcherPriority.Normal, cancellationToken)).ConfigureAwait(false);
    }

    private IClipboard? GetClipboard()
    {
        var mainWindow = _desktopLifetimeContext.MainWindow;
        if (mainWindow is null)
        {
            Log.Warning("[AvaloniaClipboard] Main window is unavailable. Clipboard access skipped.");
            return null;
        }

        var clipboard = mainWindow.Clipboard;
        if (clipboard is not null)
        {
            return clipboard;
        }

        Log.Warning("[AvaloniaClipboard] Main window clipboard is unavailable.");
        return null;
    }
}
