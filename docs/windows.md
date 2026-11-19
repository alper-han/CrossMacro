# Windows Setup

CrossMacro supports Windows desktop automation through native Windows APIs. Use
this page for installation, desktop-session expectations, and diagnostics.

## Install

Choose the channel that suits you:

- [Microsoft Store](https://apps.microsoft.com/detail/9n1qp1d6js70): managed
  installation and updates.
- [winget](install.md#winget): install from a terminal with
  `winget install AlperHan.CrossMacro`. Publication can lag behind GitHub Releases.
- [Portable EXE](install.md#portable-exe): download a self-contained `x64` or
  `arm64` file from [GitHub Releases](https://github.com/alper-han/CrossMacro/releases/latest)
  and run it directly. It does not add itself to `PATH`; use its filename for
  CLI commands or add its directory to `PATH` yourself.

GitHub MSIX artifacts are unsigned advanced/test packages, so prefer Store,
winget, or portable EXE for normal use. See the
[Windows installation guide](install.md#windows) for package details and
[release checksums](install.md#verify-downloads).

## Desktop Session

CrossMacro needs an active Windows desktop session for input, screen, and window
automation. Normal record, playback, screen capture, and window commands do not
need a CrossMacro-specific Windows privacy permission prompt.

The application registers native Windows input, screen-capture, and window
providers. A command can still fail when the current desktop is unavailable or a
target application/window rejects the requested operation.

## Behavior Notes

- **Absolute mouse moves:** playback of an absolute move fails with an error when
  Windows does not report the desktop bounds. It no longer sends the cursor to the
  top-left corner.
- **Keys:** keypad `/`, Num Lock, the Menu (Apps) key, Print Screen, and the media
  and launch keys are sent with the extended-key flag Windows expects. A key with
  no CrossMacro mapping is not recorded; the log notes it once per key.
- **Keyboard layout:** the character a key produces and the key text expansion
  types for a character both use the keyboard layout of the focused window.
- **Scroll:** recording and playback count whole wheel notches. See the scroll
  note in the [CLI reference](cli.md#platform-limitations).
- **Windows and desktops:** `window fullscreen` maximizes the active window, and
  `window workspace switch` is not supported. Both log a warning; see the
  [CLI reference](cli.md#platform-limitations).
- **Clipboard:** text is placed on the clipboard as Unicode only, with no ANSI
  `CF_TEXT` fallback.

## Diagnose A Problem

Run:

```powershell
crossmacro doctor --json --verbose
```

For a portable install that is not on `PATH`, run the downloaded executable with
the same arguments. Include the Windows version, CrossMacro version, install
channel, smallest reproducible workflow, and doctor output in a bug report.

See the [CLI reference](cli.md) for command syntax and the
[documentation index](README.md) for the other platforms.
