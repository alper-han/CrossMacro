
namespace CrossMacro.Platform.Windows.Native;

internal static partial class Shlwapi
{
    [LibraryImport("shlwapi.dll")]
    internal static partial IntPtr SHCreateMemStream(byte[] pInit, uint cbInit);
}
