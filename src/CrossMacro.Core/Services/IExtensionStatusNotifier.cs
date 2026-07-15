
namespace CrossMacro.Core.Services;

/// <summary>
/// Interface for position providers that may need external extensions
/// and want to notify UI about their status.
///
/// This allows UI to subscribe to extension status events without
/// depending on specific platform implementations.
/// </summary>
public interface IExtensionStatusNotifier
{
    /// <summary>
    /// Fired when extension status changes with structured severity code.
    /// </summary>
    public event EventHandler<ExtensionStatusChangedEventArgs>? ExtensionStatusUpdated;

    /// <summary>
    /// Legacy string-only event kept for backward compatibility.
    /// New consumers should use <see cref="ExtensionStatusUpdated"/>.
    /// </summary>
    public event EventHandler<ExtensionStatusMessageEventArgs>? ExtensionStatusChanged;

    /// <summary>
    /// Last known extension status, if one was published before a UI subscriber attached.
    /// </summary>
    public ExtensionStatusChangedEventArgs? CurrentExtensionStatus { get; }
}
