namespace CrossMacro.Platform.MacOS.Services.Input;

/// <summary>
/// Observes the distributed <c>kTISNotifySelectedKeyboardInputSourceChanged</c> notification so layout caches can be dropped
/// when the user switches keyboard layouts. The callback only invokes <c>onChanged</c>; it never touches TIS state.
/// </summary>
internal sealed class MacOSKeyboardLayoutChangeSubscription : IDisposable
{
    private const nint DeliverImmediately = 4;

    private readonly CoreFoundation.CFNotificationCallback _callback;
    private readonly IntPtr _center;
    private readonly IntPtr _observerToken;
    private readonly IntPtr _name;
    private readonly Action _onChanged;
    private int _disposed;

    private MacOSKeyboardLayoutChangeSubscription(Action onChanged, IntPtr center, IntPtr name)
    {
        _onChanged = onChanged;
        _center = center;
        _name = name;
        _callback = OnNotification;
        _observerToken = Marshal.AllocHGlobal(1);
        CoreFoundation.CFNotificationCenterAddObserver(
            center,
            _observerToken,
            Marshal.GetFunctionPointerForDelegate(_callback),
            name,
            notificationObject: IntPtr.Zero,
            DeliverImmediately);
    }

    /// <summary>Starts observing; returns <see langword="null"/> when the notification name is not exported on this system.</summary>
    internal static MacOSKeyboardLayoutChangeSubscription? Create(Action onChanged)
    {
        ArgumentNullException.ThrowIfNull(onChanged);
        var name = CoreGraphics.kTISNotifySelectedKeyboardInputSourceChanged;
        return OperatingSystem.IsMacOS() && name != IntPtr.Zero
            ? new MacOSKeyboardLayoutChangeSubscription(onChanged, CoreFoundation.CFNotificationCenterGetDistributedCenter(), name)
            : null;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) is not 0)
        {
            return;
        }

        CoreFoundation.CFNotificationCenterRemoveObserver(_center, _observerToken, _name, notificationObject: IntPtr.Zero);
        Marshal.FreeHGlobal(_observerToken);

        // The native observer holds a raw pointer to the delegate; it must outlive the registration.
        GC.KeepAlive(_callback);
    }

    private void OnNotification(IntPtr center, IntPtr observer, IntPtr name, IntPtr notificationObject, IntPtr userInfo) =>
        _onChanged();
}
