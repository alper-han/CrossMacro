namespace CrossMacro.Platform.Linux.Native.Evdev;

public sealed class EvdevInputEventArgs(UInputNative.input_event inputEvent) : EventArgs
{
    public UInputNative.input_event Event { get; } = inputEvent;
}
