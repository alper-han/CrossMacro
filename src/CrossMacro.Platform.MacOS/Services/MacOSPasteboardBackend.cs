namespace CrossMacro.Platform.MacOS.Services;

internal sealed class MacOSPasteboardBackend : IMacOSClipboardBackend
{
    public bool IsAvailable => MacOSPasteboard.IsAvailable;

    public bool TrySetText(string text) => MacOSPasteboard.TrySetText(text);

    public string? GetText() => MacOSPasteboard.GetText();

    public bool TrySetPng(byte[] pngBytes) => MacOSPasteboard.TrySetPng(pngBytes);

    public byte[]? GetPng(int maximumBytes) => MacOSPasteboard.GetPng(maximumBytes);
}
