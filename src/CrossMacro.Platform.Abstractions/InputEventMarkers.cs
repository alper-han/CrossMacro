
namespace CrossMacro.Platform.Abstractions;

/// <summary>
/// Marker values used to identify CrossMacro-generated injected input.
/// </summary>
public static class InputEventMarkers
{
    public const long TextExpansionKeyboardEvent = 0x4354584B;

    public static IntPtr ToIntPtr(long marker)
    {
        // Preserve the native-width marker conversion, including its unchecked 32-bit truncation semantics.
#pragma warning disable MA0181
        return new IntPtr(unchecked((nint)marker));
#pragma warning restore MA0181
    }
}
