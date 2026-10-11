namespace CrossMacro.Platform.Abstractions.Runtime;

/// <summary>Tracks whether the current desktop session has an operational tray host.</summary>
public interface ITrayHostMonitor : IDisposable
{
    /// <summary>Host availability alone does not establish that a native tray icon is attached.</summary>
    public bool IsAvailable { get; }

    /// <summary>Raised when host availability changes; consumers must marshal to their UI thread.</summary>
    public event EventHandler? AvailabilityChanged;

    /// <summary>Completes the bounded initial host discovery and starts observing session changes.</summary>
    public Task StartAsync(CancellationToken cancellationToken = default);
}
