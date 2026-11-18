namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

/// <summary>Delivers events the way libwayland does: through the dispatcher pointer a listener registers.</summary>
internal static class WaylandDispatcherInvoker
{
    public static int Invoke(IntPtr dispatcherPtr, uint opcode, IntPtr args = default, IntPtr target = default) =>
        Marshal.GetDelegateForFunctionPointer<WaylandProxyListener.Dispatcher>(dispatcherPtr)(IntPtr.Zero, target, opcode, IntPtr.Zero, args);

    public static int Invoke(IntPtr dispatcherPtr, uint opcode, WlArgumentPack args) =>
        Invoke(dispatcherPtr, opcode, args.Address);
}
