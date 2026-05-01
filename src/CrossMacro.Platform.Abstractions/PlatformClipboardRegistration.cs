namespace CrossMacro.Platform.Abstractions;

public sealed record PlatformClipboardRegistration(
    GuiClipboardRegistrationMode GuiMode,
    CliClipboardRegistrationMode CliMode)
{
    public static PlatformClipboardRegistration Linux { get; } =
        new(GuiClipboardRegistrationMode.LinuxShellWithAvaloniaFallback, CliClipboardRegistrationMode.LinuxShellOnly);

    public static PlatformClipboardRegistration Windows { get; } =
        new(GuiClipboardRegistrationMode.AvaloniaOnly, CliClipboardRegistrationMode.NoOp);

    public static PlatformClipboardRegistration MacOS { get; } =
        new(GuiClipboardRegistrationMode.AvaloniaOnly, CliClipboardRegistrationMode.NoOp);

    public static PlatformClipboardRegistration Default { get; } =
        new(GuiClipboardRegistrationMode.AvaloniaOnly, CliClipboardRegistrationMode.NoOp);
}

public enum GuiClipboardRegistrationMode
{
    AvaloniaOnly,
    LinuxShellWithAvaloniaFallback
}

public enum CliClipboardRegistrationMode
{
    LinuxShellOnly,
    NoOp
}
