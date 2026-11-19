namespace CrossMacro.Platform.Abstractions.Runtime;

/// <summary>
/// A no-op implementation of IExtensionStatusNotifier representing the absence of a notifier.
/// </summary>
public sealed class NullExtensionStatusNotifier : IExtensionStatusNotifier
{
    public static NullExtensionStatusNotifier Instance { get; } = new();

    public event EventHandler<ExtensionStatusChangedEventArgs>? ExtensionStatusUpdated
    {
        add { /* no-op */ }
        remove { /* no-op */ }
    }

    public ExtensionStatusChangedEventArgs? CurrentExtensionStatus => null;

    private NullExtensionStatusNotifier() { /* Empty */ }
}
