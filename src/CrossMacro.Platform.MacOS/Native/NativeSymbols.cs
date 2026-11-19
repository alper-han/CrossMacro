namespace CrossMacro.Platform.MacOS.Native;

internal static class NativeSymbols
{
    /// <summary>
    /// Reads the object pointer stored in an exported framework constant such as <c>kCFBooleanTrue</c>.
    /// The framework handle is deliberately never freed: the returned object is owned by the framework
    /// and must stay valid for the lifetime of the process.
    /// </summary>
    internal static IntPtr ReadPointer(string libraryPath, string symbolName)
    {
        return NativeLibrary.TryLoad(libraryPath, out var library)
            && NativeLibrary.TryGetExport(library, symbolName, out var address)
                ? Marshal.ReadIntPtr(address)
                : IntPtr.Zero;
    }
}
