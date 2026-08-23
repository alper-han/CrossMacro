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
- **`crossmacro-git` AUR:** tracks successful `dev` snapshots and replaces the
  stable `crossmacro` package. Use `crossmacro --version` to include its short
  source revision in bug reports.
- **Flatpak on Wayland:** direct device mode. Quick Setup can grant temporary
  direct-device ACLs without exposing the host daemon socket to the sandbox.
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
sudo gpasswd -a "$USER" crossmacro
# Log out and back in, or reboot, before starting CrossMacro again.
```

For an AD, LDAP, or SSSD identity, use its NSS-resolved login name with the
same command. This adds a local `crossmacro` group membership; it does not alter
the directory service:

```bash
sudo gpasswd -a 'directory-user' crossmacro
```

Package scripts try to add the installing identity to `crossmacro` when they can
resolve it through NSS. If package output says auto-add could not be confirmed,
run the command above manually.

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

While a daemon-backed capture session is active, the daemon rescans input event
nodes once per second. A disconnected keyboard or mouse reader is removed, and
a newly created node is opened without restarting CrossMacro or the daemon. This
recovery applies only to daemon-backed installs; portable direct-input sessions
still need Quick Setup again after replugging a device because its temporary ACL
does not automatically apply to a new `/dev/input/event*` node.

## Flatpak on Wayland

For Flatpak on Wayland, CrossMacro uses direct device mode. The portable package
does not expose or probe the host daemon socket; temporary device access is
granted to the user session when needed.

If required permissions are missing, app startup shows **Wayland Setup Required**
and can run Quick Setup automatically. Quick Setup uses `flatpak-spawn --host`
with a usable setuid `pkexec`, or `run0` as a fallback, to apply session ACLs on
the host:

- `rw` access to `/dev/uinput` or `/dev/input/uinput`
- `r` access to `/dev/input/event*`

Both host commands authorize through polkit. A Flatpak permission does not install
or provide a host polkit authentication agent. The desktop session must have a
graphical polkit agent registered; otherwise a GUI-launched setup cannot display
an authorization prompt. A `/dev/tty` or `No authentication agent found` message
means that authorization did not happen, not that `/dev/uinput` is missing. Start
the desktop's polkit agent, or run `crossmacro setup` from a terminal so selected
`pkexec` can use its textual authentication prompt.

The same setup can be requested without starting the GUI:

```bash
crossmacro setup
# Alias:
crossmacro quick-setup
```

Inside Flatpak this command uses `flatpak-spawn --host`; outside Flatpak it is
available for AppImage Wayland sessions. The command selects a usable setuid
`pkexec` or `run0` fallback, reports authorization failures, and returns exit
code `5` when the current package/session does not support temporary setup.

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

Screen-reading commands use native X11 or an available Wayland desktop capture
provider. On Wayland, CrossMacro selects the best available capture path for the
current session. Flatpak and other sandboxed runs may show a desktop capture
permission prompt, and provider availability varies by compositor and session.

If a Wayland portal route repeatedly selects the wrong backend, run
`crossmacro doctor --json --verbose`. The screen-reading details include visible
`*-portals.conf` provider evidence without opening a permission prompt. Select
the compositor's ScreenCast provider explicitly; `xdg-desktop-portal-gtk` is not
a ScreenCast provider. A persisted portal restore token reduces repeated prompts,
but the desktop portal may still request a new selection after its own session or
permission state changes.

On portal-based desktops such as GNOME, select every monitor that contains pixels
or regions the macro will read. The desktop portal owns this picker, so
CrossMacro cannot silently force a specific monitor or force all monitors to be
selected. If playback asks for a pixel outside the selected monitor coverage,
CrossMacro reports the selected bounds and requested coordinates so the capture
source can be reselected intentionally.

Image commands use the same screen capture providers as pixel and screenshot
commands. On Wayland, a requested region can be stitched from the intersecting
monitor outputs, including monitors with negative virtual coordinates. Areas in
the bounding rectangle that belong to no monitor are voids, not black pixels.
Pixel and image matching ignore those voids, and `imageclick` will not click a
match whose template crosses a void. A request with no captured monitor pixels
is reported as out of bounds. This documents the implemented capture behavior;
compositor-specific live Wayland support still depends on the available provider,
desktop permissions, and session setup.

On KDE Wayland, packaged installs include the desktop-entry permission required
for KWin screen capture. If doctor reports KWin ScreenShot2 permission denied,
verify the installed CrossMacro `.desktop` file and restart CrossMacro from the
packaged launcher.

Image matching now defaults to automatic matching with `0.95` confidence. It
starts at native scale and uses bounded correlation/pyramid and scale refinement
only when the native result is insufficient. `--matchmode first` and
`--matchmode best` (or script `matchmode first|best`) are explicit advanced
paths. Automatic matching remains the default.
A no-match is not a
monitor-gap match: CLI JSON reports a
successful `Found: false` result, while script result variables use `false` and
`-1, -1` where the command's result-variable form supports it.

The optimized matcher avoids per-candidate validity-mask scans for fully covered
Wayland frames. Compositions with monitor gaps retain an indexed validity check,
so a candidate crossing a void remains rejected. Capture/provider availability,
timeouts, and permissions still determine whether a live Wayland search can run.

## AppImage

AppImage does not install the packaged daemon-backed service. On X11, CrossMacro
uses native X11 backends when available. On Wayland, AppImage relies on direct
device fallback and may show **Linux Input Setup Required** with Quick Setup.
Quick Setup uses a usable setuid `pkexec`, or `run0` as a fallback, to grant
temporary direct device access for the current user session:

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
configured desktop identities, and `systemd.services.crossmacro`.

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

The bundled CrossMacro flake exposes the equivalent
`services.crossmacro.users` option. It grants group membership without creating
local accounts, so it supports names resolved through NSS, including AD, LDAP,
and SSSD identities. The module enables NixOS Userborn and passes these names as
members of the local `crossmacro` group. Only identities explicitly declared in
`users.users` become local accounts; directory names remain NSS-resolved group
members and are never emitted as local users. Userborn also supports immutable
user databases, so this setup does not require runtime `gpasswd` mutations. Do
not enable `systemd.sysusers` alongside the module; NixOS does not allow both
user managers at once.

## Wayland cursor positioning

CrossMacro supports Wayland with compositor-specific cursor-position
capabilities:

- When the selected compositor provider does not already publish cursor-change
  notifications and the compositor advertises `ext-image-copy-capture-v1`,
  CrossMacro uses its native cursor sessions independently of the selected
  screen capture backend. This provides logical global cursor positions without
  polling or an external helper on supporting Hyprland, GNOME, Niri, Sway,
  COSMIC, Wayfire, and other compositors. Output topology changes recreate the
  protocol sessions so their logical bounds stay current.
- Compositor-specific fallbacks are available on:
  - KDE Plasma through KWin and D-Bus cursor-change and output-topology
    notifications
  - Hyprland through activity-driven IPC queries
  - Wayfire through IPC with `ipc` and `ipc-rules` plugins, v0.10+
  - GNOME through the bundled Shell Extension
- Niri, Sway, and COSMIC releases that do not advertise the native cursor
  protocol remain resolution-only. In that case CrossMacro records raw relative
  input because those compositors do not expose a safe global cursor-position
  API.
- If an absolute cursor provider is unavailable, CrossMacro falls back to
  relative-position mode for recording.
- Macros that contain logical desktop coordinates, whether absolute positions
  or logical relative deltas, require an absolute-capable playback backend.
- **Force Relative Coordinates** records direct raw-relative deltas by default.
  Enable **Logical Relative Pixels** to record logical desktop-pixel deltas;
  that option is unavailable when no global cursor-position provider exists.
- You can disable the origin move at recording start with
  **Skip Initial 0,0 Position**.

Absolute and relative coordinate events can be mixed in one macro.
Current-position clicks do not carry coordinates and execute at the live cursor
position.

Run scripts can save one live cursor sample with
`mouse position <x_variable> <y_variable>`. The values use signed logical
desktop coordinates and can feed later moves, conditions, or loop logic. This
does not create a separate background cursor: later move/click steps still share
the user's pointer. The step fails on sessions that do not expose a global
cursor-position provider, including Niri, Sway, and affected COSMIC sessions.

Force-relative recordings and editor-authored **Relative (Raw Input)** actions
use raw device deltas and direct relative playback. Enable the recording tab's
**Logical Relative Pixels** option, use the explicit **Relative (Logical
Pixels)** editor mode, or use `rel-logical` script mode when exact logical
desktop-pixel deltas are needed; playback converts those deltas to logical
targets, so pointer acceleration does not distort their path.
Movement-only logical-relative macros re-anchor to a manually moved live cursor
position. Logical-relative movement that precedes a click, button, drag, image,
or screen-coordinate action remains strict: its target must settle before the
dependent action runs.

The native Wayland cursor connection is recreated automatically when outputs,
scale, transform, seat capabilities, or relevant protocol globals change. On
X11, root-window coordinates provide the same logical path reconstruction
without compositor-specific helpers.

Absolute playback treats the daemon acknowledgement and compositor observation
as separate steps. The daemon waits for a newly created uinput event node before
the first batch, and playback waits up to 250 ms for Wayland/X11 to publish the
requested logical position. The normal path returns on the first matching
observation; the bound only protects a click from racing a delayed compositor
update and does not add a fixed delay to every move.

On GNOME releases without the native cursor protocol, the bundled Shell
Extension supplies absolute mouse position. It supports GNOME Shell 45 through
51. CrossMacro reports extension status through its setup flow and diagnostics.
Log out and back in after first-time setup if prompted.

## Minimal systems and conflicts

Daemon authorization and Quick Setup flows may require `polkit`, `pkcheck`, and
`pkexec` on minimal systems. Portable Quick Setup also requires a graphical polkit
authentication agent for GUI-launched authorization:

```bash
which pkcheck pkexec
pkcheck --version
```

Install your distro's polkit package if these tools are missing.
If authorization reports `No authentication agent found` or a `/dev/tty` error,
start the desktop's polkit agent before retrying.

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
