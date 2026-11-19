
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.PortalPipeWire;

/// <summary>
/// Invokes <c>pw_core</c> methods through the interface's method table.
/// </summary>
/// <remarks>
/// <c>pw_core_add_listener</c> and <c>pw_core_sync</c> are C macros in the PipeWire headers that dispatch through
/// <c>struct pw_core_methods</c>; libpipewire does not export them on every release, so they are called the
/// same way the macros do. Every member after <c>version</c> is a function pointer, so a method's position in the
/// table is its pointer-sized slot index.
/// </remarks>
internal static class PipeWireCoreMethods
{
    private const int NotSupported = -95;
    private const int AddListenerSlot = 1;
    private const int SyncSlot = 3;

    internal delegate int AddListenerMethod(IntPtr target, IntPtr listener, IntPtr events, IntPtr data);

    internal delegate int SyncMethod(IntPtr target, uint id, int sequence);

    public static int AddListener(IntPtr core, IntPtr listener, IntPtr events, IntPtr data)
    {
        var method = Resolve(core, AddListenerSlot, out var target);
        return method == IntPtr.Zero
            ? NotSupported
            : Marshal.GetDelegateForFunctionPointer<AddListenerMethod>(method)(target, listener, events, data);
    }

    public static int Sync(IntPtr core, uint id, int sequence)
    {
        var method = Resolve(core, SyncSlot, out var target);
        return method == IntPtr.Zero
            ? NotSupported
            : Marshal.GetDelegateForFunctionPointer<SyncMethod>(method)(target, id, sequence);
    }

    private static IntPtr Resolve(IntPtr core, int slot, out IntPtr target)
    {
        var callbacks = Marshal.PtrToStructure<SpaInterface>(core).Callbacks;
        target = callbacks.Data;
        return callbacks.Functions == IntPtr.Zero ? IntPtr.Zero : Marshal.ReadIntPtr(callbacks.Functions, slot * IntPtr.Size);
    }
}
