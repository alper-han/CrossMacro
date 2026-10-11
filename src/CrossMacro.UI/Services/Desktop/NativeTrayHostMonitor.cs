namespace CrossMacro.UI.Services.Desktop;

/// <summary>
/// Native Windows and macOS trays do not require a separately registered desktop host.
/// The tray service also verifies that Avalonia attached a native icon.
/// </summary>
internal sealed class NativeTrayHostMonitor : ITrayHostMonitor
{
    private bool _disposed;

    public bool IsAvailable { get; private set; }

    public event EventHandler? AvailabilityChanged;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsAvailable)
        {
            IsAvailable = true;
            AvailabilityChanged?.Invoke(this, EventArgs.Empty);
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        IsAvailable = false;
        AvailabilityChanged?.Invoke(this, EventArgs.Empty);
    }
}
