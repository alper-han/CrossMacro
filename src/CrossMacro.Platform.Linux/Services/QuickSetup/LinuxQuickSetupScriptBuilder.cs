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

        if (options.RequireUInputDevice)
        {
            script.Append("uinput_ok=0; ");
        }

        script.Append("for p in /dev/uinput /dev/input/uinput; do ");
        script.Append("if [ -e \"$p\" ]; then setfacl -m \"u:${TARGET_IDENTITY}:rw\" \"$p\"; ");
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
        script.Append("if [ -e \"$p\" ]; then setfacl -m \"u:${TARGET_IDENTITY}:r\" \"$p\"; ");
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

        return script.ToString();
    }
}
