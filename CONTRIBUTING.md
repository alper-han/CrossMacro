# Contributing to CrossMacro

Thanks for taking the time to improve CrossMacro. This guide covers the basics
for reporting issues, setting up a development environment, verifying changes,
and opening pull requests.

CrossMacro is a cross-platform desktop automation app. Changes often affect more
than one surface: GUI, CLI, platform permissions, Linux daemon/direct-device
setup, packaging, and documentation. Please call out the affected platforms and
install channels clearly when reporting issues or opening pull requests.

## Reporting Bugs

- Use the **Bug Report** issue template.
- Include your platform, OS version, install method, and CrossMacro version.
- Include the desktop/session details when relevant, for example Linux X11,
  GNOME/KDE/Hyprland Wayland, Windows session state, or macOS version.
- Include reproduction steps and relevant logs.
- Include doctor output whenever possible:

  ```bash
  crossmacro doctor --json --verbose
  ```

  DMG installs on macOS do not usually add `crossmacro` to your shell `PATH`.
  Use the app-bundle executable instead:

  ```bash
  /Applications/CrossMacro.app/Contents/MacOS/CrossMacro.UI doctor --json --verbose
  ```

For Linux daemon-backed installs, daemon logs are often useful:

```bash
sudo systemctl kill -s USR1 crossmacro.service
journalctl -u crossmacro.service -f
```

Send `USR1` again to restore the normal daemon log level.

## Suggesting Enhancements

- Use the **Feature Request** issue template.
- Describe the workflow or problem you want to solve.
- Mention the affected platform(s), install channel, and whether the feature
  touches recording, playback, shortcuts, scheduling, text expansion, CLI,
  platform permissions, or packaging.

## Security Reports

Do not report vulnerabilities as public issues. Use GitHub private vulnerability
reporting or the contact address in [SECURITY.md](SECURITY.md).

## Development Setup

### Prerequisites

- .NET 10 SDK
- Git
- Platform-specific tools for the area you are changing

This repository does not require optional .NET workloads. The shared build
properties disable the workload resolver so minimal SDK environments, including
Nix-based environments, can build the project.

### Restore and build

From the repository root:

```bash
dotnet restore
dotnet build --configuration Debug --no-restore
```

### Run the app from source

Use the platform entry project. The shared `src/CrossMacro.UI` project is not the
top-level executable.

```bash
# Linux
dotnet run --project src/CrossMacro.UI.Linux/CrossMacro.UI.Linux.csproj --

# Windows
dotnet run --project src/CrossMacro.UI.Windows/CrossMacro.UI.Windows.csproj --

# macOS
dotnet run --project src/CrossMacro.UI.MacOS/CrossMacro.UI.MacOS.csproj --
```

CLI commands are routed through the platform app executable:

```bash
LINUX_APP=src/CrossMacro.UI.Linux/CrossMacro.UI.Linux.csproj
dotnet run --project "$LINUX_APP" -- --help
dotnet run --project "$LINUX_APP" -- doctor --json --verbose
```

For CLI syntax and direct-run examples, see [docs/cli.md](docs/cli.md).

## Platform Notes

### Linux

Linux input automation depends on the session, install channel, and permission
path. See [docs/linux.md](docs/linux.md) for daemon-backed mode, direct-device
fallback, Flatpak, AppImage, NixOS, Wayland cursor positioning, and debug logs.

For local daemon development you can install the daemon from the repository:

```bash
sudo ./scripts/daemon/install.sh
```

This script builds and installs the daemon, creates the `crossmacro` system user
and group, installs udev/polkit files, sets up `crossmacro.service`, and adds the
installing user to the `crossmacro` group when it can identify that user. Log out
and back in, or reboot, after group changes.

Linux platform tests may need a D-Bus session:

```bash
LINUX_TESTS=tests/CrossMacro.Platform.Linux.Tests/CrossMacro.Platform.Linux.Tests.csproj
dbus-run-session -- dotnet test "$LINUX_TESTS" --configuration Debug --no-build
```

### Windows

No additional setup is usually required for basic build/run. Windows capture and
playback use platform APIs directly. If you change Windows packaging, also check
MSIX and winget-related files.

### macOS

macOS recording/global shortcuts require Input Monitoring. Playback/input
injection requires Accessibility. See [docs/macos.md](docs/macos.md) for the DMG
install and permission flow.

Source builds can be launched with `dotnet run`, but release DMG installs run
from `/Applications/CrossMacro.app` and are not normally added to shell `PATH`.

## Testing and Verification

Run the smallest relevant test set for your change, then run broader checks when
touching shared behavior.

Common checks:

```bash
dotnet restore
dotnet build --configuration Debug --no-restore
CORE_TESTS=tests/CrossMacro.Core.Tests/CrossMacro.Core.Tests.csproj
CLI_TESTS=tests/CrossMacro.Cli.Tests/CrossMacro.Cli.Tests.csproj
INFRA_TESTS=tests/CrossMacro.Infrastructure.Tests/CrossMacro.Infrastructure.Tests.csproj
UI_TESTS=tests/CrossMacro.UI.Tests/CrossMacro.UI.Tests.csproj
dotnet test "$CORE_TESTS" --configuration Debug --no-build
dotnet test "$CLI_TESTS" --configuration Debug --no-build
dotnet test "$INFRA_TESTS" --configuration Debug --no-build
dotnet test "$UI_TESTS" --configuration Debug --no-build
```

Platform-specific test projects:

```bash
WINDOWS_TESTS=tests/CrossMacro.Platform.Windows.Tests/CrossMacro.Platform.Windows.Tests.csproj
MACOS_TESTS=tests/CrossMacro.Platform.MacOS.Tests/CrossMacro.Platform.MacOS.Tests.csproj
LINUX_TESTS=tests/CrossMacro.Platform.Linux.Tests/CrossMacro.Platform.Linux.Tests.csproj
DAEMON_TESTS=tests/CrossMacro.Daemon.Tests/CrossMacro.Daemon.Tests.csproj
dotnet test "$WINDOWS_TESTS" --configuration Debug --no-build
dotnet test "$MACOS_TESTS" --configuration Debug --no-build
dbus-run-session -- dotnet test "$LINUX_TESTS" --configuration Debug --no-build
dotnet test "$DAEMON_TESTS" --configuration Debug --no-build
```

CLI smoke checks:

```bash
LINUX_APP=src/CrossMacro.UI.Linux/CrossMacro.UI.Linux.csproj
bash scripts/smoke/cli-smoke.sh \
  --command "dotnet run --no-build --project $LINUX_APP --"
pwsh -NoProfile -File scripts/smoke/cli-smoke.ps1 -Project src/CrossMacro.UI.Windows/CrossMacro.UI.Windows.csproj
```

CI also validates workflow policy, package metadata, Flatpak/AppStream files,
the CLI manpage, version sync, platform builds, and package smoke checks. If you
change packaging or release logic, run the relevant script-level checks locally
where possible.

## Documentation Changes

Update user-facing docs together with behavior changes:

- `README.md` for the project overview, install matrix, and support links.
- `docs/linux.md` for Linux runtime modes, permissions, Wayland/X11 behavior,
  NixOS, Flatpak, AppImage, and daemon troubleshooting.
- `docs/macos.md` for macOS install, Gatekeeper, Input Monitoring, and
  Accessibility behavior.
- `docs/cli.md` and `docs/man/crossmacro.1` for CLI command or option changes.
- Package metadata and store manifests when install channels, permissions, or
  release assets change.

## Packaging and Release Work

Most contributors do not need to run the full release pipeline locally. If your
change touches package metadata, install scripts, release assets, or versioned
manifests, check the relevant files and scripts before opening a PR.

Do not bump release versions unless a maintainer asks you to. Version changes,
release tags, and store publishing are maintainer-owned release steps.

Useful entry points:

```bash
./scripts/sync-version.sh --check
bash scripts/ci/resolve-release-metadata.sh --mode ci
man -l docs/man/crossmacro.1 > /dev/null
```

Packaging-related areas include:

- `flatpak/`
- `scripts/build_*.sh`
- `scripts/packaging/`
- `scripts/msix/`
- `scripts/ci/`
- `.github/workflows/`

Use the pull request template to call out affected channels such as Flatpak,
AppImage, `.deb`, `.rpm`, AUR, Nix/NixOS, MSIX/winget, and DMG.

## Pull Requests

1. Fork the repository and create your branch from `dev`.
2. Keep changes focused and follow existing code style and naming patterns.
3. Run relevant build, test, smoke, and documentation checks.
4. Fill out the pull request template carefully.
5. Call out affected platforms, install channels, permission/daemon changes,
   screenshots, logs, and breaking changes when relevant.

The GitHub workflows validate source, CLI, packaging-adjacent metadata, and
platform-specific test suites. Passing CI is required, but local verification
before opening a PR makes review much faster.
