namespace CrossMacro.Platform.MacOS.Services.Clipboard;

internal interface IMacOSClipboardBackend
{
    public bool IsAvailable { get; }

    public bool TrySetText(string text);

    public string? GetText();

    public bool TrySetPng(byte[] pngBytes);

    public byte[]? GetPng(int maximumBytes);
}
