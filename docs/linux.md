# Linux Setup and Troubleshooting

CrossMacro supports Linux on Wayland and X11, but input automation depends on
the install channel, desktop session, and available permissions. Start with the
doctor command before changing groups, ACLs, or service permissions:

```bash
crossmacro doctor --json --verbose
```

Doctor reports daemon-backed readiness separately from direct device readiness.
Direct device checks can pass while daemon IPC still warns or fails, for example
when `/run/crossmacro/crossmacro.sock` exists but the current login session has
not picked up `crossmacro` group membership.

## Install mode quick map

- **`.deb`, `.rpm`, AUR:** daemon-backed packages. Package scripts set up
  `crossmacro.service` and the `crossmacro` group.
- **Flatpak on Wayland:** daemon-backed or direct device fallback. CrossMacro
  uses the host daemon socket when exposed; Quick Setup can grant temporary
  direct-device ACLs.
- **AppImage on X11:** native X11 backend using XInput2/XTest when available.
- **AppImage on Wayland:** direct device fallback. Quick Setup may prompt for
  temporary input permissions.
- **NixOS module from nixpkgs:** daemon-backed setup. The module installs the UI
  package, configures the daemon package, enables uinput, installs udev and
  polkit files, creates the `crossmacro` group and service user, adds configured
  users to the group, and starts `systemd.services.crossmacro`.

## Linux runtime modes

CrossMacro supports two Linux input modes:

- **Daemon-backed mode:** the preferred packaged mode. The app talks to
  `crossmacro.service` over `/run/crossmacro/crossmacro.sock`, while the daemon
  service user owns Linux device access.
- **Direct device mode:** a fallback for channels such as AppImage on Wayland
  and some sandbox scenarios. The app process needs access to `/dev/uinput` and,
  for recording or hotkeys, readable `/dev/input/event*` devices.

On X11, CrossMacro tries native X11 capture and playback first. A supported
native X11 session uses XInput2/XTest and does not require daemon-backed mode,
`/dev/uinput`, or `/dev/input/event*` permissions. Linux input permissions only
matter on X11 if native X11 backends are unavailable and CrossMacro falls back
to daemon/direct Linux input paths.

## Daemon-backed packages

After installing `.deb`, `.rpm`, AUR, or the NixOS module, make sure your desktop
user belongs to the `crossmacro` group. That group grants access to the daemon
socket, not to raw input devices:

```bash
sudo usermod -aG crossmacro "$USER"
# Log out and back in, or reboot, before starting CrossMacro again.
```

Package scripts try to add the installing user to `crossmacro` when they can
identify that user. If package output says auto-add could not be confirmed, run
the command above manually.

Daemon packages also install the daemon user, udev rules, polkit files, and
uinput setup where supported by the package scripts.

If your environment skips service setup, for example on non-systemd or chroot
installs, start the service manually:

```bash
sudo systemctl enable --now crossmacro.service
```

Do not weaken daemon socket permissions as a workaround. Use doctor output to
identify whether the failing path is daemon-backed mode, direct device mode, or
both.

If doctor reports daemon socket, daemon group, service, or handshake problems:

```bash
systemctl status crossmacro.service
groups | grep crossmacro
sudo systemctl enable --now crossmacro.service
```

If doctor reports daemon device access problems, verify the packaged service and
uinput setup before changing service-user groups:

```bash
lsmod | grep uinput
sudo modprobe uinput
id crossmacro
stat -c '%A %a %U:%G %n' /dev/uinput
sudo -u crossmacro test -w /dev/uinput && echo writable || echo not-writable
```

The packaged daemon service is expected to keep device access through
package-provided service, udev, module, and group configuration. Treat manual
`input` or `uinput` group changes for the service user as repair steps, not
normal setup.

## Flatpak on Wayland

For Flatpak on Wayland, CrossMacro uses a hybrid startup path:

- **Daemon-backed mode** when the host daemon socket is exposed at
  `/run/crossmacro/crossmacro.sock`
- **Direct device mode** when the daemon path is unavailable and temporary
  device access is granted to the user session

If required permissions are missing, app startup shows **Wayland Setup Required**
and can run Quick Setup automatically. Quick Setup uses
`flatpak-spawn --host pkexec` to apply session ACLs on the host:

- `rw` access to `/dev/uinput` or `/dev/input/uinput`
- `r` access to `/dev/input/event*`

If Quick Setup is denied or fails, use doctor first. Manual ACL fallback, run on
the Linux host rather than inside the Flatpak sandbox:

```bash
sudo modprobe uinput
for p in /dev/uinput /dev/input/uinput; do \
  [ -e "$p" ] && sudo setfacl -m "u:$USER:rw" "$p"; \
done
for p in /dev/input/event*; do \
  [ -e "$p" ] && sudo setfacl -m "u:$USER:r" "$p"; \
done
```

If `setfacl` is missing, install your distro's `acl` package first.

## Linux screen reading

Screen-reading commands are supported on native X11 and Wayland desktop
sessions. On Wayland, CrossMacro uses the best available desktop capture path for
the current session. Flatpak and other sandboxed runs may show a desktop capture
permission prompt.

On portal-based desktops such as GNOME, select every monitor that contains pixels
or regions the macro will read. The desktop portal owns this picker, so
CrossMacro cannot silently force a specific monitor or force all monitors to be
selected. If playback asks for a pixel outside the selected monitor coverage,
CrossMacro reports the selected bounds and requested coordinates so the capture
source can be reselected intentionally.

On KDE Wayland, packaged installs include the desktop-entry permission required
for KWin screen capture. If doctor reports KWin ScreenShot2 permission denied,
verify the installed CrossMacro `.desktop` file and restart CrossMacro from the
packaged launcher.

## AppImage

AppImage does not install the packaged daemon-backed service. On X11, CrossMacro
uses native X11 backends when available. On Wayland, AppImage relies on direct
device fallback and may show **Linux Input Setup Required** with Quick Setup.
Quick Setup uses `pkexec` to grant temporary direct device access for the current
user session:

- `rw` access to `/dev/uinput` or `/dev/input/uinput`
- `r` access to `/dev/input/event*`

These temporary ACLs may need to be applied again after reboot or device
re-enumeration.

Run the AppImage:

```bash
chmod +x CrossMacro-*.AppImage
./CrossMacro-*.AppImage
```

Permanent setup is optional and should be treated as advanced manual
configuration because adding a user to `input` grants broad access to input
devices:

```bash
sudo tee /etc/udev/rules.d/99-crossmacro.rules >/dev/null <<'EOF'
KERNEL=="uinput", GROUP="input", MODE="0660", OPTIONS+="static_node=uinput"
EOF
sudo udevadm control --reload-rules && sudo udevadm trigger
sudo usermod -aG input "$USER"
# Log out and back in, or reboot, before starting CrossMacro again.
```

## NixOS

For NixOS, use the official nixpkgs module instead of installing only the UI
package. The module provides the full daemon-backed setup: UI package, daemon
package, uinput, udev rules, polkit files, `crossmacro` group and service user,
configured desktop users, and `systemd.services.crossmacro`.

Minimal NixOS configuration:

```nix
{
  services.crossmacro = {
    enable = true;
    users = [ "yourusername" ];
  };
}
```

Available module options include:

- `services.crossmacro.enable`
- `services.crossmacro.package`
- `services.crossmacro.daemonPackage`
- `services.crossmacro.users`

After switching, log out and back in, or reboot, so your desktop session picks up
the `crossmacro` group membership.

## Wayland cursor positioning

CrossMacro supports Wayland with compositor-specific cursor-position
capabilities:

- Absolute cursor position is available on:
  - Hyprland through IPC
  - Wayfire through IPC with `ipc` and `ipc-rules` plugins, v0.10+
  - KDE Plasma through D-Bus
  - GNOME through a Shell Extension
- Niri and COSMIC are detected for screen resolution only; they do not currently
  expose a safe absolute cursor-position API for recording.
- If an absolute cursor provider is unavailable, CrossMacro falls back to
  relative-position mode for recording.
- Macros that contain absolute-coordinate events require an absolute-capable
  backend for playback.
- You can force relative mode with **Force Relative Coordinates**.
- You can disable the origin move at recording start with
  **Skip Initial 0,0 Position**.

Absolute and relative coordinate events can be mixed in one macro.
Current-position clicks do not carry coordinates and execute at the live cursor
position.

For the smoothest relative-position playback, disable pointer acceleration and
use a flat pointer profile in your desktop or compositor settings; accelerated
profiles can distort replayed movement deltas.

GNOME Wayland needs a Shell Extension for absolute mouse position. The bundled
extension supports GNOME Shell 45 through 50. CrossMacro reports extension status
through its setup flow and diagnostics. Log out and back in after first-time
setup if prompted.

## Minimal systems and conflicts

Daemon authorization and Quick Setup flows may require `polkit`, `pkcheck`, and
`pkexec` on minimal systems:

```bash
which pkcheck pkexec
pkcheck --version
```

Install your distro's polkit package if these tools are missing.

Some applications can lock input devices exclusively. If capture or playback
behaves inconsistently, pause conflicting tools, for example GPU Screen Recorder,
test CrossMacro again, then resume them.

## Debug logging

For daemon-backed Linux installs, toggle daemon debug logging with `USR1`:

```bash
sudo systemctl kill -s USR1 crossmacro.service
journalctl -u crossmacro.service -f
```

Send `USR1` again to restore normal log level.
