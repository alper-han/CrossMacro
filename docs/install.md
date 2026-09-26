# Download And Install

[CrossMacro](../README.md) · [Documentation index](README.md)

Choose a channel for your platform and update preference. For direct downloads,
use [GitHub Releases](https://github.com/alper-han/CrossMacro/releases/latest),
select the artifact matching your architecture, and download `SHA256SUMS` from
the same release to [verify it](#verify-downloads).

## Download Matrix

Replace `<version>` with the release version. The filenames below distinguish
architectures; they are not shell commands.

| Platform / channel | Download or package | Before you start |
| --- | --- | --- |
| Linux — [Flatpak / Flathub](https://flathub.org/apps/io.github.alper_han.crossmacro) | `io.github.alper_han.crossmacro` | Sandboxed; Wayland may request temporary Quick Setup. |
| Linux — Debian / Ubuntu | `crossmacro-<version>_amd64.deb` or `crossmacro-<version>_arm64.deb` | Native package; re-login or reboot after group changes. |
| Linux — Fedora / RHEL | `crossmacro-<version>-1.x86_64.rpm` or `crossmacro-<version>-1.aarch64.rpm` | Native package; re-login or reboot after group changes. |
| Linux — [AUR stable](https://aur.archlinux.org/packages/crossmacro) | `crossmacro` | Stable daemon-backed package. |
| Linux — [AUR development](https://aur.archlinux.org/packages/crossmacro-git) | `crossmacro-git` | Tracks successful `dev` snapshots; replaces and conflicts with stable `crossmacro`. |
| Linux — AppImage | `CrossMacro-<version>-x86_64.AppImage` or `CrossMacro-<version>-aarch64.AppImage` | Run directly; Wayland may require temporary Quick Setup. |
| Linux — [NixOS module](https://search.nixos.org/options?channel=unstable&query=services.crossmacro) | `services.crossmacro` | Use a supported nixpkgs channel or the repository's flake module; configure your desktop users and read the [module constraints](linux.md#nixos). |
| Windows — [Microsoft Store](https://apps.microsoft.com/detail/9n1qp1d6js70) | CrossMacro Store app | Simplest Windows install with managed updates. |
| Windows — winget | `AlperHan.CrossMacro` | Stable publication channel; availability can lag behind GitHub Releases. |
| Windows — portable EXE | `CrossMacro-<version>-win-x64.exe` or `CrossMacro-<version>-win-arm64.exe` | Self-contained; does not add itself to `PATH`. |
| Windows — GitHub MSIX | `CrossMacro-<version>-x64.msix` or `CrossMacro-<version>-arm64.msix` | Unsigned advanced/test artifacts, unlike the recommended Store installation; prefer Store, winget, or portable EXE. |
| macOS — DMG | `CrossMacro-<version>-osx-arm64.dmg` or `CrossMacro-<version>-osx-x64.dmg` | macOS 14+; unsigned and not notarized. Gatekeeper may require **Open Anyway**. |

All `.deb`, `.rpm`, AppImage, EXE, MSIX, and DMG files above are available from
[GitHub Releases](https://github.com/alper-han/CrossMacro/releases/latest).

## Linux

CrossMacro supports Wayland and X11. Native packages (`.deb`, `.rpm`, AUR, and
the NixOS module) provide the daemon-backed setup used on Wayland. Flatpak and
AppImage instead use direct device access on Wayland and do not use the host
daemon. Native X11 input is used when available.

### Flatpak

```bash
flatpak install flathub io.github.alper_han.crossmacro
```

On Wayland, approve Quick Setup if requested. To request setup from a host
terminal without starting the GUI:

```bash
flatpak run io.github.alper_han.crossmacro setup
```

See [Flatpak on Wayland](linux.md#flatpak-on-wayland) for authorization,
temporary device permissions, and diagnostics. Screen-aware workflows also
depend on the desktop's [screen capture setup](linux.md#linux-screen-reading).

### Debian / Ubuntu And Fedora / RHEL

Download the matching package first, then run the appropriate command from its
directory:

```bash
# Debian or Ubuntu
sudo apt install ./crossmacro*.deb

# Fedora or RHEL
sudo dnf install ./crossmacro*.rpm
```

If installation added your user to the `crossmacro` group, log out and back in
or reboot before using CrossMacro. See [daemon-backed package setup](linux.md#daemon-backed-packages)
for manual group membership, service activation, and non-systemd constraints.

### Arch Linux / AUR

Choose **one** package:

```bash
# Stable
yay -S crossmacro

# Development (replaces stable)
yay -S crossmacro-git
```

You can use `paru -S crossmacro` or `paru -S crossmacro-git` instead.
`crossmacro-git` follows successful `dev` snapshots, not the stable release
channel, and conflicts with `crossmacro`. Include the revision printed by
`crossmacro --version` in development-package bug reports.

These are daemon-backed packages; follow the same
[group and service setup](linux.md#daemon-backed-packages) as other native packages.

### AppImage

Download the architecture-matching AppImage and make it executable. Run these
commands in a directory containing only the AppImage you intend to launch:

```bash
chmod +x CrossMacro-*.AppImage
./CrossMacro-*.AppImage
```

If Wayland device permissions are missing, use Quick Setup in the app or run:

```bash
./CrossMacro-*.AppImage setup
```

Quick Setup permissions are temporary and may need to be applied again after
reboot or device re-enumeration. See [AppImage setup](linux.md#appimage) for
diagnostics and advanced configuration.

### NixOS

Prefer `services.crossmacro` when it is available in your nixpkgs channel, or
use the equivalent module exported by this repository's flake:

```nix
services.crossmacro = {
  enable = true;
  users = [ "yourusername" ];
};
```

Configure your desktop users, rebuild, and log out and back in or reboot to pick
up group membership. The bundled flake module supports NSS-resolved AD, LDAP,
and SSSD identities without creating local accounts for them. It enables
Userborn; do not enable `systemd.sysusers` alongside it. Read the canonical
[NixOS module reference](linux.md#nixos) for options and identity-management
constraints before changing your configuration.

## Windows

Choose Microsoft Store, winget, or a portable EXE for normal use. All three are
supported installation options; you do not need the Store to use CrossMacro.

### Microsoft Store

The [Microsoft Store](https://apps.microsoft.com/detail/9n1qp1d6js70) provides a
managed installation with updates.

### winget

Install from a terminal:

```powershell
winget install AlperHan.CrossMacro
```

winget follows the stable publication channel and can appear later than a
GitHub Release.

### Portable EXE

Download a self-contained EXE from
[GitHub Releases](https://github.com/alper-han/CrossMacro/releases/latest):

- `CrossMacro-<version>-win-x64.exe` for x64.
- `CrossMacro-<version>-win-arm64.exe` for ARM64.

Run the downloaded file directly. It does not add itself to `PATH`; for CLI
commands, use its filename or add its directory to `PATH` yourself. See
[Verify downloads](#verify-downloads) for the release checksum file.

### MSIX Packages (Advanced)

GitHub MSIX files are unsigned advanced/test packages, not the recommended
end-user installation path. These are distinct from the Microsoft Store
installation; prefer Store, winget, or portable EXE for normal use.

See [Windows desktop-session requirements](windows.md#desktop-session) and
[Windows diagnostics](windows.md#diagnose-a-problem) if automation is unavailable.

## macOS

Current .NET 10 builds support **macOS 14 or newer**. Download `osx-arm64` for
Apple Silicon or `osx-x64` for Intel, open the DMG, and drag CrossMacro to
Applications. Launch the installed app from Applications.

GitHub DMGs are **unsigned and not notarized**. If Gatekeeper blocks the app,
follow the [Gatekeeper guide](troubleshooting/macos-gatekeeper.md) for **Open
Anyway** and targeted troubleshooting.

Recording and global shortcuts require **Input Monitoring**; playback uses the
**Accessibility** approval flow. Screen-aware automation additionally requires
**Screen Recording**. Follow the illustrated [macOS setup guide](macos.md)
for installation and [privacy permissions](macos.md#grant-permissions).

DMG installs do not normally add `crossmacro` to your shell `PATH`. Use the
[app-bundle executable](macos.md#troubleshooting) for terminal commands.

## Verify Downloads

Download `SHA256SUMS` from the same GitHub release as your artifacts. On Linux,
place it alongside the downloaded files and run in that directory:

```bash
sha256sum --ignore-missing -c SHA256SUMS
```

This checks the release files present in the current directory and ignores
entries for files you did not download. Check the output for your downloaded
files; their checksums must match before you use them.

## Permissions And Desktop Readiness

Windows and native X11 sessions normally need no extra permission prompt.
macOS requires privacy permissions; Linux Wayland may require device or
screen-portal setup depending on the package and compositor. Input, cursor,
screen, tray, and window-management capabilities depend on the operating system
and active desktop session.

If something is unavailable after installation, inspect the active environment:

```bash
crossmacro doctor --json --verbose
```

Portable channels do not necessarily provide a `crossmacro` command on `PATH`.
Run the downloaded executable directly with these arguments, or add its
directory to `PATH` yourself. For a macOS DMG install, use the
[app-bundle command](macos.md#troubleshooting) instead.

Continue with the [Linux reference](linux.md#start-here),
[Windows diagnostics](windows.md#diagnose-a-problem), or
[macOS troubleshooting](macos.md#troubleshooting). Once setup is ready, return
to [your first macro](../README.md#your-first-macro).
