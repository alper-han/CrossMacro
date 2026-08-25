# Linux Platform Reference

> [!NOTE]
> This is the canonical Linux installation, capability, and troubleshooting
> reference for CrossMacro.

CrossMacro supports both Wayland and X11. You should not need to guess which
backend or permission model your system uses: the install channel and desktop
session determine the best path, and CrossMacro reports that decision through
its diagnostics.

## Start Here

Run this before changing groups, ACLs, services, or device permissions:

```bash
crossmacro doctor --json --verbose
```

Read the report by capability, not as one overall pass/fail result. In particular,
daemon-backed input and direct-device input are independent. One may be ready
while the other is unavailable, and that can be completely normal for your
package.

Choose your path below, then return to the deeper troubleshooting sections only
if the basic setup does not work.

### Symptom Map

| Symptom | First check |
| --- | --- |
| Daemon socket or handshake failure | Confirm `crossmacro.service` is running and log out and back in after any `crossmacro` group change. |
| Flatpak or AppImage input unavailable on Wayland | Run the package-specific Quick Setup command and make sure a polkit authentication agent is available. |
| Portable input unavailable on X11 | Confirm the application has access to the active `DISPLAY` and that native XInput2/XTest is available. Quick Setup is not the X11 remedy. |
| Wayland screen capture unavailable | Inspect `doctor` for the selected provider; on Flatpak, also verify the Portal ScreenCast backend, PipeWire, and monitor selection. |
| Absolute recording unavailable | Read [Wayland cursor positioning](#wayland-cursor-positioning); a usable input backend alone does not provide a live global cursor position. |
| Input changes after reconnecting a device | Repeat Quick Setup for portable Wayland sessions; its temporary ACLs do not apply to newly created event nodes. |
| Window commands return an environment error | Check [Linux window control](#linux-window-control); only specific compositors currently provide a window backend. |

## Pick Your Install Type

| Install | Normal input path | What you usually need to do |
| --- | --- | --- |
| `.deb`, `.rpm`, or AUR | Native X11 first on X11; CrossMacro daemon on Wayland | Log out and back in if the installer added you to the `crossmacro` group |
| NixOS module | Native X11 first on X11; CrossMacro daemon on Wayland | Add your desktop user to `services.crossmacro.users`, then rebuild and re-login |
| Flatpak on X11 | Native XInput2/XTest | Usually no Linux device permission setup |
| Flatpak on Wayland | Direct device access | Approve Quick Setup; the Flatpak session gate requires writable uinput and readable event devices |
| AppImage on X11 | Native XInput2/XTest | Usually no Linux device permission setup |
| AppImage on Wayland | Direct device access | Run Quick Setup; repeat it after reboot or device re-enumeration if needed |

The `crossmacro-git` AUR package follows successful `dev` snapshots and replaces
the stable `crossmacro` package. Include the revision printed by
`crossmacro --version` in bug reports.

## Linux Runtime Modes

CrossMacro has three input paths:

- **Native X11:** XInput2 records input and XTest sends it. This is the first
  choice in a usable X11 session and does not need daemon or raw-device access.
- **Daemon-backed mode:** the preferred native-package Wayland path. The app talks
  to `crossmacro.service` over `/run/crossmacro/crossmacro.sock`, while the
  daemon service user owns Linux device access.
- **Direct device mode:** Flatpak and AppImage do not select or probe the host
  daemon. On Wayland, they use direct uinput access. Playback needs writable
  `/dev/uinput` or `/dev/input/uinput`; recording and global hotkeys additionally
  need readable `/dev/input/event*` devices.

No one path proves that another is ready. For example, a portable Wayland
session can play input with direct uinput while recording remains unavailable
because event devices are unreadable. Run `doctor` in the desktop session you
intend to automate.

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

If a systemd-based installation skipped service activation, enable it manually:

```bash
sudo systemctl enable --now crossmacro.service
```

CrossMacro's packaged daemon currently targets systemd. Running it in a
non-systemd environment requires service-manager-specific integration outside
the normal package setup. A chroot or image build should not try to start the
service until the package is installed on the target system.

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

## Flatpak On Wayland

Flatpak does not expose or probe the host daemon socket. On Wayland, CrossMacro
uses direct device mode: playback needs writable uinput, while recording and
global hotkeys also need readable event devices. The Flatpak display-session gate
requires both before treating a Wayland session as ready.

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
the desktop's polkit agent, or run the package-specific setup command from a
terminal so selected `pkexec` can use its textual authentication prompt.

Request Flatpak setup without starting the GUI:

```bash
flatpak run io.github.alper_han.crossmacro setup
```

The command uses `flatpak-spawn --host`, selects a usable setuid `pkexec` or
`run0` fallback, reports authorization failures, and returns exit code `5` when
the current package/session does not support temporary setup.

After setup, test recording and playback separately:

```bash
flatpak run io.github.alper_han.crossmacro doctor --json --verbose
```

If Quick Setup is denied or fails, use doctor first. The manual ACL fallback
below runs on the Linux host rather than inside the Flatpak sandbox.

> [!CAUTION]
> Read access to `/dev/input/event*` can expose keyboard and other input events
> to every process running as your user. Prefer Quick Setup and apply these ACLs
> only when you understand that scope.

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

## Linux Screen Reading

Screen-reading commands use native X11 capture on X11. Native Wayland builds
prefer compositor-specific providers before Portal: KDE tries KWin ScreenShot2,
then ext-image-copy and wlr-screencopy; other sessions try the GNOME extension,
then ext-image-copy and wlr-screencopy. Portal is the fallback in both orders.

Flatpak Wayland intentionally uses **only** XDG Desktop Portal ScreenCast. It
does not select KWin ScreenShot2, the GNOME extension, ext-image-copy capture, or
wlr-screencopy. Portal availability, its selected ScreenCast backend, PipeWire,
monitor selection, and desktop permission state determine support. AppImage is
not subject to this Flatpak-only capture policy; it follows the native Wayland
provider order while using direct input on Wayland.

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

On KDE Wayland, native packages and AppImage include the desktop-entry permission
required for KWin ScreenShot2 capture. Flatpak intentionally uses Portal instead.
If a native/AppImage doctor report says KWin ScreenShot2 permission was denied,
verify the installed CrossMacro `.desktop` file and restart CrossMacro from the
packaged launcher.

Image matching defaults to automatic matching with `0.95` confidence. A no-match
is not a monitor-gap match: CLI JSON reports a successful `found: false` result,
while script result variables use `false` and `-1, -1` where supported. See the
[CLI reference](cli.md) for matching modes, timeout behavior, and result
contracts.

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

Request setup without starting the GUI:

```bash
./CrossMacro-*.AppImage setup
```

After setup, test recording and playback separately:

```bash
./CrossMacro-*.AppImage doctor --json --verbose
```

Run the AppImage:

```bash
chmod +x CrossMacro-*.AppImage
./CrossMacro-*.AppImage
```

Permanent setup is optional and should be treated as advanced manual
configuration. Adding a user to `input` grants every process running as that
user broad access to input devices, and event-device group policy varies by
distribution. Verify device ownership before relying on this method:

```bash
sudo tee /etc/udev/rules.d/99-crossmacro.rules >/dev/null <<'EOF'
KERNEL=="uinput", GROUP="input", MODE="0660", OPTIONS+="static_node=uinput"
EOF
sudo udevadm control --reload-rules && sudo udevadm trigger
sudo usermod -aG input "$USER"
# Log out and back in, or reboot, before starting CrossMacro again.
```

After signing in again, verify both `/dev/uinput` and the required
`/dev/input/event*` nodes with `stat` or `getfacl`. If the event nodes are not
accessible through `input`, use temporary Quick Setup instead of loosening
global device permissions.

## NixOS

For NixOS, prefer the `services.crossmacro` module when it is available in your
nixpkgs channel, or use the equivalent module exported by this repository's
flake. The module provides the full daemon-backed setup: UI package, daemon
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

## Wayland Cursor Positioning

Absolute recording and absolute playback are separate capabilities. Input access
alone does not reveal the live global pointer position, so CrossMacro selects a
cursor provider independently of its input and screen-capture providers.

- CrossMacro attempts the native Wayland ext-image-copy cursor provider when a
  selected compositor provider does not already publish cursor changes. It is
  independent of the selected screen-capture provider. A usable session needs
  the required Wayland globals, a pointer-capable seat, usable outputs, and at
  least one live cursor-position event. Negotiating the protocol alone is not
  enough; for example, Sway can expose it while a software cursor produces no
  live sample.
- KDE Plasma uses KWin and D-Bus cursor/output notifications. Hyprland uses
  activity-driven IPC queries. Wayfire uses IPC with the `ipc` and `ipc-rules`
  plugins, version 0.10 or later. GNOME can use the bundled Shell Extension when
  the native cursor provider is unavailable.
- Niri IPC, Sway IPC, and `cosmic-randr` provide display geometry, not a global
  cursor position. Current Niri sessions record pointer movement as raw-relative;
  absolute recording is not available there. Native Sway and COSMIC sessions can
  use CrossMacro's independent native cursor provider when it produces a live
  position. Do not treat native COSMIC as categorically relative-only.
- Native COSMIC supports absolute recording and maps absolute playback through
  its output topology. It needs an absolute-capable input backend and usable
  output geometry; multi-output routing also needs a live cursor position to
  identify the active output. Disconnected layouts cannot be routed across.
- Macros containing absolute positions or logical-relative deltas need an
  absolute-capable playback backend. **Force Relative Coordinates** records raw
  device deltas by default. **Logical Relative Pixels** records logical desktop
  deltas and requires a live global cursor provider while recording.
- **Skip Initial 0,0 Position** disables the origin move at recording start.

### Flatpak Cursor Limits

Flatpak is a separate runtime boundary. Flatpak COSMIC currently does not expose
the native cursor path CrossMacro needs for absolute recording, so it records
raw-relative movement. This does not describe native COSMIC behavior. Use
`crossmacro doctor --json --verbose` from the affected package and desktop
session for the active cursor provider and coordinate mode.

Absolute and relative coordinate events can be mixed in one macro.
Current-position clicks do not carry coordinates and execute at the live cursor
position.

Run scripts can save one live cursor sample with
`mouse position <x_variable> <y_variable>`. The values use signed logical
desktop coordinates and can feed later moves, conditions, or loop logic. This
does not create a separate background cursor: later move/click steps still share
the user's pointer. The step fails when the current session has no live global
cursor-position provider.

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

## Linux Window Control

Window commands require a registered window-manager backend. They never fall
back to another desktop; unsupported sessions return an environment error.

| Desktop | Current backend requirement |
| --- | --- |
| Hyprland | Accessible Hyprland IPC socket |
| Sway | Accessible `SWAYSOCK` |
| Niri | Accessible validated `NIRI_SOCKET` |
| KDE Plasma | KDE desktop session with the KWin backend available, on X11 or Wayland |
| GNOME | GNOME desktop session with the bundled extension/tracker backend available, on X11 or Wayland |
| Other X11 desktops, COSMIC, Wayfire, and other desktops | No CrossMacro window-manager backend is currently registered |

Flatpak has no separate window-control policy in CrossMacro. A supported backend
still needs access to its compositor socket or session D-Bus service from the
sandbox, so use `doctor` and the specific command in the target session.

## Minimal Systems And Conflicts

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

## Debug Logging

For daemon-backed Linux installs, toggle daemon debug logging with `USR1`:

```bash
sudo systemctl kill -s USR1 crossmacro.service
journalctl -u crossmacro.service -f
```

Send `USR1` again to restore normal log level.
