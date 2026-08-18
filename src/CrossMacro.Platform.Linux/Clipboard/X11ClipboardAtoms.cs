namespace CrossMacro.Platform.Linux.Clipboard;

internal sealed class X11ClipboardAtoms
{
    private X11ClipboardAtoms(IntPtr display)
    {
        Clipboard = X11Native.XInternAtom(display, "CLIPBOARD", only_if_exists: false);
        Atom = X11Native.XInternAtom(display, "ATOM", only_if_exists: false);
        Targets = X11Native.XInternAtom(display, "TARGETS", only_if_exists: false);
        Utf8String = X11Native.XInternAtom(display, "UTF8_STRING", only_if_exists: false);
        Text = X11Native.XInternAtom(display, "TEXT", only_if_exists: false);
        String = X11Native.XInternAtom(display, "STRING", only_if_exists: false);
        TextPlainUtf8 = X11Native.XInternAtom(display, "text/plain;charset=utf-8", only_if_exists: false);
        TextPlain = X11Native.XInternAtom(display, "text/plain", only_if_exists: false);
        ImagePng = X11Native.XInternAtom(display, "image/png", only_if_exists: false);
        Incr = X11Native.XInternAtom(display, "INCR", only_if_exists: false);
        Property = X11Native.XInternAtom(display, "_CROSSMACRO_CLIPBOARD", only_if_exists: false);
    }

    public nuint Clipboard { get; }
    public nuint Atom { get; }
    public nuint Targets { get; }
    public nuint Utf8String { get; }
    public nuint Text { get; }
    public nuint String { get; }
    public nuint TextPlainUtf8 { get; }
    public nuint TextPlain { get; }
    public nuint ImagePng { get; }
    public nuint Incr { get; }
    public nuint Property { get; }

    public static X11ClipboardAtoms Create(IntPtr display) => new(display);
}
