namespace CrossMacro.Platform.Linux.Services.Factories;

/// <summary>
/// Shared backend choice for capture and simulation: native X11 first, then daemon or direct device
/// according to the capability snapshot, otherwise an explicit "unavailable" backend.
/// </summary>
internal static class LinuxInputBackendResolver
{
    internal static TBackend Resolve<TBackend, TX11>(
        LinuxCapabilitySnapshot snapshot,
        ILinuxCapabilitySnapshotProvider snapshotProvider,
        bool forCapture,
        Func<TX11> createNativeX11,
        Func<TX11, bool> isNativeX11Supported,
        Func<TBackend> createDirect,
        Func<TBackend> createDaemon,
        Func<LinuxCapabilitySnapshot, TBackend> createUnavailable)
        where TX11 : class, TBackend
        where TBackend : class, IDisposable
    {
        var x11 = snapshot.IsX11 ? createNativeX11() : null;
        var retainX11 = false;
        try
        {
            var nativeX11Supported = x11 is not null && isNativeX11Supported(x11);
            var selection = LinuxBackendSelectionPolicy.SelectInput(snapshot, nativeX11Supported, forCapture);

            if (selection.Backend is not LinuxInputBackend.NativeX11 && selection.Mode is not InputProviderMode.Daemon)
            {
                // External setup may have granted or revoked device access since the cached snapshot.
                snapshotProvider.InvalidateDirectInputCache();
                snapshot = snapshotProvider.GetSnapshot();
                selection = LinuxBackendSelectionPolicy.SelectInput(snapshot, nativeX11Supported: false, forCapture);
            }

            if (selection.Backend is LinuxInputBackend.NativeX11)
            {
                retainX11 = true;
                return x11!;
            }

            return selection.Mode switch
            {
                InputProviderMode.Daemon => createDaemon(),
                InputProviderMode.DirectDevice => createDirect(),
                InputProviderMode.None => createUnavailable(snapshot),
                _ => createUnavailable(snapshot),
            };
        }
        finally
        {
            if (!retainX11)
            {
                x11?.Dispose();
            }
        }
    }

    /// <param name="directFallbackShortfall">What is missing on the direct-device side, phrased to follow "and ".</param>
    internal static string BuildUnavailableMessage(
        LinuxCapabilitySnapshot snapshot,
        string subject,
        string directFallbackShortfall)
    {
        var daemonReason = DescribeDaemonFailure(snapshot.Input.DaemonHandshake.Status);

        return $"No usable Linux input {subject} is available: {daemonReason} and {directFallbackShortfall}.";
    }

    private static string DescribeDaemonFailure(LinuxDaemonHandshakeStatus status)
    {
        if (status is LinuxDaemonHandshakeStatus.PermissionDenied)
        {
            return "daemon socket permission denied";
        }

        if (status is LinuxDaemonHandshakeStatus.Timeout)
        {
            return "daemon handshake timed out";
        }

        return status is LinuxDaemonHandshakeStatus.MissingSocket
            ? "daemon socket is missing"
            : "daemon backend unavailable";
    }
}