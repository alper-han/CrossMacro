namespace CrossMacro.UI.ViewModels.Editor;

/// <summary>Cancels only capture operations acquired by this document.</summary>
internal sealed class EditorCaptureSession(TimeProvider timeProvider) : IDisposable
{
    private readonly Lock _gate = new();
    private EditorCaptureLease? _active;
    private bool _disposed;

    internal EditorCaptureLease Begin()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_active is not null) { throw new InvalidOperationException("A document capture is already active."); }
            _active = new EditorCaptureLease(new CancellationTokenSource(TimeSpan.FromSeconds(30), timeProvider), Release);
            return _active;
        }
    }

    internal void Cancel()
    {
        lock (_gate)
        {
            if (_active is null) { return; }
            _active.Cancel();
        }
    }

    private void Release(EditorCaptureLease lease)
    {
        lock (_gate) { if (ReferenceEquals(_active, lease)) { _active = null; } }
    }

    public void Dispose()
    {
        lock (_gate) { if (_disposed) { return; } _disposed = true; Cancel(); }
    }
}
