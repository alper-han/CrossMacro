namespace CrossMacro.Platform.Linux.Native.Evdev;

public sealed class EvdevErrorEventArgs(Exception exception) : EventArgs
{
    public Exception Exception { get; } = exception;
}
