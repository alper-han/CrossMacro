
namespace CrossMacro.Platform.MacOS.Native;

internal static class LibSystem
{
    private const string LibSystemLib = "/usr/lib/libSystem.dylib";

    [DllImport(LibSystemLib)]
    public static extern int pthread_main_np();
}
