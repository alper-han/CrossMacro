
namespace CrossMacro.UI.Services.Runtime;

/// <summary>
/// Shows startup dialogs before the main window exists, using a transparent owner window
/// so the dialogs can be centered and the desktop lifetime stays alive.
/// </summary>
internal sealed class BootstrapDialogHost(IUiDispatcher uiDispatcher)
{
    private readonly IUiDispatcher _uiDispatcher = uiDispatcher ?? throw new ArgumentNullException(nameof(uiDispatcher));

    internal static ConfirmationDialog CreateCenteredConfirmationDialog(
        string title,
        string message,
        string yesText,
        string? noText,
        bool dangerYes = false,
        bool dangerNo = false)
    {
        return new ConfirmationDialog(
            title,
            message,
            yesText,
            noText,
            dangerYes,
            dangerNo)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
        };
    }

    internal async Task RunWithOwnerAsync(
        IClassicDesktopStyleApplicationLifetime desktop,
        Func<Window, Task> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(desktop);
        ArgumentNullException.ThrowIfNull(action);

        cancellationToken.ThrowIfCancellationRequested();
        var bootstrapOwner = await _uiDispatcher.InvokeAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var owner = CreateOwnerWindow();
            desktop.MainWindow = owner;
            owner.Show();
            return owner;
        }).ConfigureAwait(false);

        using var cancellationRegistration = cancellationToken.Register(() =>
            _uiDispatcher.Post(bootstrapOwner.Close));
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await action(bootstrapOwner).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
        finally
        {
            try
            {
                await _uiDispatcher.InvokeAsync(bootstrapOwner.Close).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not OperationCanceledException)
            {
                Log.Debug(ex, "[BootstrapDialogHost] Bootstrap owner close was skipped.");
            }
        }
    }

    internal async Task<T> ShowDialogAsync<T>(Window owner, Func<Window> createDialog)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(createDialog);

        return await (await _uiDispatcher.InvokeAsync(() => createDialog().ShowDialog<T>(owner)).ConfigureAwait(false)).ConfigureAwait(false);
    }

    internal Task ShutdownAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        ArgumentNullException.ThrowIfNull(desktop);
        return _uiDispatcher.InvokeAsync(() => desktop.Shutdown());
    }

    /// <summary>Replaces the desktop main window with a message dialog whose only action exits the app.</summary>
    internal void ShowTerminalDialog(
        IClassicDesktopStyleApplicationLifetime desktop,
        string title,
        string message,
        string exitText,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(desktop);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        _uiDispatcher.Post(() =>
        {
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            var dialog = new ConfirmationDialog(title, message, exitText, noText: null);
            desktop.MainWindow = dialog;
            dialog.Show();
        });
    }

    private static Window CreateOwnerWindow()
    {
        return new Window
        {
            Width = 1,
            Height = 1,
            Opacity = 0,
            ShowInTaskbar = false,
            CanResize = false,
            WindowDecorations = WindowDecorations.None,
            WindowStartupLocation = WindowStartupLocation.Manual,
        };
    }
}
