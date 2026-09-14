namespace CrossMacro.Platform.Linux.Native.UInput;

internal interface IUInputEventWriter
{
    public void WriteEvent(ushort type, ushort code, int value);
}
