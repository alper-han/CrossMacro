namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.PortalPipeWire;

internal sealed class PipeWireFrameAdmission(bool requiresSettlingFrame)
{
    private readonly int _requiredUsableFrames = requiresSettlingFrame ? 2 : 1;
    private int _usableFrameCount;

    public bool AcceptsNextUsableFrame() =>
        Interlocked.Increment(ref _usableFrameCount) >= _requiredUsableFrames;
}
