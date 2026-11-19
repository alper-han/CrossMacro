
namespace CrossMacro.Platform.Windows.Native;

internal static partial class Ole32
{
    /// <summary>RPC_E_CHANGED_MODE: the thread is already initialized with a different COM apartment model.</summary>
    internal const int RpcEChangedMode = unchecked((int)0x80010106);

    [LibraryImport("ole32.dll")]
    internal static partial int OleInitialize(IntPtr pvReserved);

    [LibraryImport("ole32.dll")]
    internal static partial void OleUninitialize();
}
