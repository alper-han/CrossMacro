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

    public static UInputAbsoluteMovePlan CreateAbsoluteMovePlan(
        (int X, int Y)? current,
        (int X, int Y) target,
        int width,
        int height)
    {
        if (current != target)
        {
            return new UInputAbsoluteMovePlan(target, Reassertion: null);
        }

        if (width > 1)
        {
            var x = target.X < width - 1 ? target.X + 1 : target.X - 1;
            return new UInputAbsoluteMovePlan(target, (x, target.Y));
        }

        if (height > 1)
        {
            var y = target.Y < height - 1 ? target.Y + 1 : target.Y - 1;
            return new UInputAbsoluteMovePlan(target, (target.X, y));
        }

        return new UInputAbsoluteMovePlan(target, Reassertion: null);
    }
}
