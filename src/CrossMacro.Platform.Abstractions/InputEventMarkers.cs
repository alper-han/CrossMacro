using System;

namespace CrossMacro.Platform.Abstractions;

/// <summary>
/// Marker values used to identify CrossMacro-generated injected input.
/// </summary>
public static class InputEventMarkers
{
    public const long TextExpansionKeyboardEvent = 0x4354584B;

    public static IntPtr ToIntPtr(long marker)
    {
        return new IntPtr(unchecked((nint)marker));
    }
}
