namespace CrossMacro.Platform.Linux.Native.UInput;

/// <summary>
/// Keeps coordinate capability and clamping rules independent from uinput syscalls.
/// The policy is internal because the public <see cref="IUInputDevice"/> contract remains
/// the compatibility boundary for platform consumers.
/// </summary>
internal static class UInputDeviceCoordinatePolicy
{
    public static bool SupportsAbsoluteCoordinates(int width, int height) => width > 0 && height > 0;

    public static (int X, int Y) ClampAbsoluteCoordinates(int x, int y, int width, int height)
    {
        if (width > 0)
        {
            x = Math.Clamp(x, 0, width - 1);
        }

        if (height > 0)
        {
            y = Math.Clamp(y, 0, height - 1);
        }

        return (x, y);
    }

    public static (int X, int Y) GetAbsoluteMaximums(int width, int height)
    {
        return SupportsAbsoluteCoordinates(width, height)
            ? (width - 1, height - 1)
            : (0, 0);
    }
}
