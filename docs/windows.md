# Windows Setup

CrossMacro supports Windows desktop automation through native Windows APIs. Use
this page for installation, desktop-session expectations, and diagnostics.

## Install

Choose one of these channels:

```powershell
# Managed install and updates
winget install AlperHan.CrossMacro
```

The [Microsoft Store](https://apps.microsoft.com/detail/9n1qp1d6js70) is the
simplest managed-update option. GitHub Releases also provides portable `x64` and
`arm64` EXE files; run a portable file directly unless you add its directory to
`PATH`. GitHub MSIX artifacts are unsigned advanced/test packages, so prefer the
Store, `winget`, or the portable EXE for normal use.

## Desktop Session

CrossMacro needs an active Windows desktop session for input, screen, and window
automation. Normal record, playback, screen capture, and window commands do not
need a CrossMacro-specific Windows privacy permission prompt.

The application registers native Windows input, screen-capture, and window
providers. A command can still fail when the current desktop is unavailable or a
target application/window rejects the requested operation.

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
