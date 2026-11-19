namespace CrossMacro.Platform.Linux.Clipboard;

/// <summary>One XGetWindowProperty reply.</summary>
/// <param name="Type">The property type atom.</param>
/// <param name="Bytes">The returned items. Format-32 items are Xlib <c>long</c>s, so each takes <c>sizeof(nint)</c> bytes.</param>
/// <param name="WireBytesRead">How many bytes of the property this reply consumed on the wire (items times format / 8).</param>
/// <param name="BytesAfter">How many bytes of the property remain after this reply.</param>
internal readonly record struct X11PropertyChunk(nuint Type, byte[] Bytes, long WireBytesRead, long BytesAfter);
