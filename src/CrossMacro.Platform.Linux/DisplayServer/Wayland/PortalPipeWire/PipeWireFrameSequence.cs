namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.PortalPipeWire;

internal sealed class PipeWireFrameSequence
{
    private long _value;

    public long Snapshot() => Interlocked.Read(ref _value);

    public long BeginProcess() => Interlocked.Increment(ref _value);

    public static bool IsNewerThan(long frameGeneration, long requestGeneration) => frameGeneration > requestGeneration;
}
