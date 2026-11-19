namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

/// <summary>
/// A listener for an auxiliary proxy of an owner that already listens on another one
/// (a seat next to a registry, an xdg-output next to an output).
/// </summary>
internal sealed class WaylandCallbackListener(Action<uint, IntPtr> onEvent) : WaylandProxyListener
{
    private readonly Action<uint, IntPtr> _onEvent = onEvent;

    protected override void OnEvent(IntPtr target, uint opcode, IntPtr args) => _onEvent(opcode, args);
}
