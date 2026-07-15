namespace CrossMacro.Platform.Linux.Services;

public readonly record struct LinuxBackendSelection(
    InputProviderMode Mode,
    bool CaptureSupported,
    string Reason)
{
    public bool IsSupported => Reason is "native-x11" || Mode is not InputProviderMode.None;
}

/// <summary>
/// Pure Linux input backend policy. It intentionally keeps capture and simulation
/// distinct because direct-device simulation does not require readable event devices.
/// </summary>
public static class LinuxBackendSelectionPolicy
{
    public static LinuxBackendSelection SelectInput(
        LinuxCapabilitySnapshot snapshot,
        bool nativeX11Supported,
        bool forCapture)
    {
        if (snapshot.IsX11 && nativeX11Supported)
        {
            return new LinuxBackendSelection(InputProviderMode.None, CaptureSupported: true, "native-x11");
        }

        var mode = snapshot.Input.ResolvedMode ?? (snapshot.Input.DaemonHandshakeSucceeded
            ? InputProviderMode.Daemon
            : snapshot.Input.CanUseDirectUInput
                ? InputProviderMode.Legacy
                : InputProviderMode.None);

        if (forCapture && mode is InputProviderMode.Legacy && !snapshot.Input.CanReadInputEvents)
        {
            return new LinuxBackendSelection(InputProviderMode.None, CaptureSupported: false, "direct-input-events-unavailable");
        }

        return new LinuxBackendSelection(mode, mode is not InputProviderMode.None, mode switch
        {
            InputProviderMode.Daemon => "daemon",
            InputProviderMode.Legacy => "direct-device",
            _ => "no-backend",
        });
    }
}
