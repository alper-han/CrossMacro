namespace CrossMacro.Platform.Linux.Clipboard;

internal sealed class X11IncrementalTransfer(
    nuint requestor,
    nuint property,
    nuint type,
    byte[] data)
{
    public nuint Requestor { get; } = requestor;
    public nuint Property { get; } = property;
    public nuint Type { get; } = type;
    public byte[] Data { get; } = data;
    public int Offset { get; set; }
}
