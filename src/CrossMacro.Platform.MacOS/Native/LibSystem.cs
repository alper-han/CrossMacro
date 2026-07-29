
namespace CrossMacro.Platform.MacOS.Native;

internal static partial class LibSystem
{
    private const string LibSystemLib = "/usr/lib/libSystem.dylib";

    [LibraryImport(LibSystemLib)]
    public static partial int pthread_main_np();
}
