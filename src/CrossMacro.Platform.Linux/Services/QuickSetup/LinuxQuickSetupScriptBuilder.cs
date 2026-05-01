using System.Text;

namespace CrossMacro.Platform.Linux.Services.QuickSetup;

internal sealed class LinuxQuickSetupScriptBuilder
{
    public string Build(LinuxQuickSetupScriptOptions options)
    {
        var script = new StringBuilder();
        script.Append("set -eu; ");
        script.Append("TARGET_IDENTITY=\"$1\"; ");
        script.Append("if ! command -v setfacl >/dev/null 2>&1; then ");
        script.Append("echo 'setfacl is missing on host. Install ACL package and retry.' >&2; ");
        script.Append("exit 22; ");
        script.Append("fi; ");
        script.Append("if command -v modprobe >/dev/null 2>&1; then modprobe uinput >/dev/null 2>&1 || true; fi; ");
        script.Append("uinput_count=0; ");
        script.Append("event_count=0; ");

        if (options.RequireUInputDevice)
        {
            script.Append("uinput_ok=0; ");
        }

        script.Append("for p in /dev/uinput /dev/input/uinput; do ");
        script.Append("if [ -e \"$p\" ]; then setfacl -m \"u:${TARGET_IDENTITY}:rw\" \"$p\"; uinput_count=$((uinput_count + 1)); ");
        if (options.RequireUInputDevice)
        {
            script.Append("uinput_ok=1; ");
        }
        script.Append("fi; ");
        script.Append("done; ");

        if (options.RequireUInputDevice)
        {
            script.Append("if [ \"$uinput_ok\" -ne 1 ]; then ");
            script.Append("echo 'uinput device is not available. Load the uinput module and retry.' >&2; ");
            script.Append("exit 24; ");
            script.Append("fi; ");
        }

        if (options.RequireInputEvents)
        {
            script.Append("event_ok=0; ");
        }

        script.Append("for p in /dev/input/event*; do ");
        script.Append("if [ -e \"$p\" ]; then setfacl -m \"u:${TARGET_IDENTITY}:r\" \"$p\"; event_count=$((event_count + 1)); ");
        if (options.RequireInputEvents)
        {
            script.Append("event_ok=1; ");
        }
        script.Append("fi; ");
        script.Append("done; ");

        if (options.RequireInputEvents)
        {
            script.Append("if [ \"$event_ok\" -ne 1 ]; then ");
            script.Append("echo 'No /dev/input/event* devices were found for session ACL setup.' >&2; ");
            script.Append("exit 25; ");
            script.Append("fi; ");
        }

        script.Append("if [ \"$uinput_count\" -eq 0 ] && [ \"$event_count\" -eq 0 ]; then ");
        script.Append("echo 'Quick setup could not find /dev/uinput or /dev/input/event* on host for session ACL setup.' >&2; ");
        script.Append("exit 26; ");
        script.Append("fi; ");
        script.Append("printf '%s\\n' \"Applied session ACLs for ${TARGET_IDENTITY}: uinput=${uinput_count}, input-events=${event_count}.\"; ");

        return script.ToString();
    }
}
