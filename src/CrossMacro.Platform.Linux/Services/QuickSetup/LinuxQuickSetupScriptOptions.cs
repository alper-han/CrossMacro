namespace CrossMacro.Platform.Linux.Services.QuickSetup;

internal readonly record struct LinuxQuickSetupScriptOptions(bool RequireUInputDevice, bool RequireInputEvents)
{
    public static LinuxQuickSetupScriptOptions Lenient => new(RequireUInputDevice: false, RequireInputEvents: false);

    public static LinuxQuickSetupScriptOptions Strict => new(RequireUInputDevice: true, RequireInputEvents: true);
}
