
namespace CrossMacro.Platform.Linux.Services.QuickSetup;

internal static class LinuxQuickSetupScriptBuilder
{
    public static string Build(LinuxQuickSetupScriptOptions options)
    {
        var script = new StringBuilder();
        AppendPreamble(script);
        AppendUInputSection(script, options);
        AppendInputEventsSection(script, options);
        AppendFinalGuard(script);
        return script.ToString();
    }

    private static void AppendPreamble(StringBuilder script)
    {
        _ = script.Append("set -eu; ");
        _ = script.Append("TARGET_IDENTITY=\"$1\"; ");
        _ = script.Append("if ! command -v setfacl >/dev/null 2>&1; then ");
        _ = script.Append("echo 'setfacl is missing on host. Install ACL package and retry.' >&2; ");
        _ = script.Append("exit 22; ");
        _ = script.Append("fi; ");
        _ = script.Append("if command -v modprobe >/dev/null 2>&1; then modprobe uinput >/dev/null 2>&1 || true; fi; ");
        _ = script.Append("uinput_count=0; ");
        _ = script.Append("event_count=0; ");
    }

    private static void AppendUInputSection(StringBuilder script, LinuxQuickSetupScriptOptions options)
    {
        if (options.RequireUInputDevice)
        {
            _ = script.Append("uinput_ok=0; ");
        }

        _ = script.Append("for p in /dev/uinput /dev/input/uinput; do ");
        _ = script.Append("if [ -e \"$p\" ]; then setfacl -m \"u:${TARGET_IDENTITY}:rw\" \"$p\"; uinput_count=$((uinput_count + 1)); ");
        if (options.RequireUInputDevice)
        {
            _ = script.Append("uinput_ok=1; ");
        }
        _ = script.Append("fi; ");
        _ = script.Append("done; ");

        if (options.RequireUInputDevice)
        {
            _ = script.Append("if [ \"$uinput_ok\" -ne 1 ]; then ");
            _ = script.Append("echo 'uinput device is not available. Load the uinput module and retry.' >&2; ");
            _ = script.Append("exit 24; ");
            _ = script.Append("fi; ");
        }
    }

    private static void AppendInputEventsSection(StringBuilder script, LinuxQuickSetupScriptOptions options)
    {
        if (options.RequireInputEvents)
        {
            _ = script.Append("event_ok=0; ");
        }

        _ = script.Append("for p in /dev/input/event*; do ");
        _ = script.Append("if [ -e \"$p\" ]; then setfacl -m \"u:${TARGET_IDENTITY}:r\" \"$p\"; event_count=$((event_count + 1)); ");
        if (options.RequireInputEvents)
        {
            _ = script.Append("event_ok=1; ");
        }
        _ = script.Append("fi; ");
        _ = script.Append("done; ");

        if (options.RequireInputEvents)
        {
            _ = script.Append("if [ \"$event_ok\" -ne 1 ]; then ");
            _ = script.Append("echo 'No /dev/input/event* devices were found for session ACL setup.' >&2; ");
            _ = script.Append("exit 25; ");
            _ = script.Append("fi; ");
        }
    }

    private static void AppendFinalGuard(StringBuilder script)
    {
        _ = script.Append("if [ \"$uinput_count\" -eq 0 ] && [ \"$event_count\" -eq 0 ]; then ");
        _ = script.Append("echo 'Quick setup could not find /dev/uinput or /dev/input/event* on host for session ACL setup.' >&2; ");
        _ = script.Append("exit 26; ");
        _ = script.Append("fi; ");
        _ = script.Append("printf '%s\\n' \"Applied session ACLs for ${TARGET_IDENTITY}: uinput=${uinput_count}, input-events=${event_count}.\"; ");
    }
}
