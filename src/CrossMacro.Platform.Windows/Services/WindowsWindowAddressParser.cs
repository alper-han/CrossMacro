namespace CrossMacro.Platform.Windows.Services;

/// <summary>
/// Keeps the public window-address parsing contract independent from User32 calls.
/// </summary>
internal static class WindowsWindowAddressParser
{
    internal static bool TryParse(string address, out IntPtr hwnd)
    {
        hwnd = IntPtr.Zero;
        if (!long.TryParse(address, CultureInfo.InvariantCulture, out var handleValue))
        {
            return false;
        }

        hwnd = new IntPtr(handleValue);
        return hwnd != IntPtr.Zero;
    }
}
