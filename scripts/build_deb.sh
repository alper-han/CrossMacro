#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/lib/version.sh
source "$SCRIPT_DIR/lib/version.sh"
# shellcheck source=scripts/lib/platform.sh
source "$SCRIPT_DIR/lib/platform.sh"

# Configuration
APP_NAME="crossmacro"
VERSION="$(get_version)"
PACKAGE_VERSION="$(to_filename_version)"
DEB_VERSION="$(to_deb_version)"
TARGET_ARCH_RESOLVED="$(get_target_arch)"
ARCH="${DEB_ARCH:-$(to_deb_arch "$TARGET_ARCH_RESOLVED")}"
DOTNET_ARCH="$(to_dotnet_arch "$TARGET_ARCH_RESOLVED")"
DAEMON_RID="linux-$DOTNET_ARCH"
ELF_INTERPRETER="${ELF_INTERPRETER:-$(get_glibc_interpreter "$TARGET_ARCH_RESOLVED")}"
PUBLISH_DIR="${PUBLISH_DIR:-../publish}"  # Use env var or default to ../publish
DEB_DIR="deb_package"
ICON_PATH="../src/CrossMacro.UI/Assets/mouse-icon.png"
MANPAGE_SOURCE="../docs/man/crossmacro.1"
OUTPUT_DEB="${APP_NAME}-${DEB_VERSION}_${ARCH}.deb"

# Clean previous build
rm -rf "$DEB_DIR" "$OUTPUT_DEB"

# Verify publish directory exists
if [ ! -d "$PUBLISH_DIR" ]; then
    echo "Error: Publish directory not found: $PUBLISH_DIR"
    echo "Please build the application first or set PUBLISH_DIR environment variable"
    exit 1
fi

UI_SOURCE_BINARY="$PUBLISH_DIR/CrossMacro.UI"
if [ ! -f "$UI_SOURCE_BINARY" ]; then
    echo "Error: UI binary not found in publish directory: $UI_SOURCE_BINARY"
    exit 1
fi
verify_binary_arch "$UI_SOURCE_BINARY" "$TARGET_ARCH_RESOLVED"

echo "Using pre-built binaries from: $PUBLISH_DIR"
echo "Packaging architecture: $ARCH (target: $TARGET_ARCH_RESOLVED)"
echo "Daemon publish RID: $DAEMON_RID"

# 2. Create Directory Structure
echo "Creating directory structure..."
mkdir -p "$DEB_DIR/DEBIAN"
mkdir -p "$DEB_DIR/usr/bin"
mkdir -p "$DEB_DIR/usr/lib/$APP_NAME"
mkdir -p "$DEB_DIR/usr/lib/$APP_NAME/daemon"
mkdir -p "$DEB_DIR/usr/lib/systemd/system"
mkdir -p "$DEB_DIR/usr/lib/udev/rules.d"
mkdir -p "$DEB_DIR/usr/share/applications"
mkdir -p "$DEB_DIR/usr/share/icons/hicolor"
mkdir -p "$DEB_DIR/usr/share/man/man1"


# 3. Create Control File
echo "Creating control file..."
cat > "$DEB_DIR/DEBIAN/control" << EOF
Package: $APP_NAME
Version: $DEB_VERSION
Section: utils
Priority: optional
Architecture: $ARCH
Depends: libc6, libstdc++6, polkitd | policykit-1, libxtst6, zlib1g, libssl3 | libssl1.1, libsystemd0, libxkbcommon0
Recommends: libx11-6, libice6, libsm6, libfontconfig1
Maintainer: Zynix <crossmacro@zynix.net>
Description: Mouse and Keyboard Macro Automation Tool
 A powerful cross-platform mouse and keyboard macro automation tool.
 Supports text expansion and works on Linux (Wayland/X11), Windows, and macOS.
 Includes background input daemon for secure macro playback.
EOF

# Create postinst script
cat > "$DEB_DIR/DEBIAN/postinst" << EOF
#!/bin/bash
set -e

if [ "\$1" = "configure" ]; then
    # Create group if not exists (Debian-idiomatic)
    if ! getent group crossmacro >/dev/null; then
        addgroup --system crossmacro || true
    fi

    if ! getent group input >/dev/null; then
        addgroup --system input || true
    fi

    if ! getent group uinput >/dev/null; then
        addgroup --system uinput || true
    fi

    # Create user if not exists
    if ! getent passwd crossmacro >/dev/null; then
        adduser --system --no-create-home --ingroup input --disabled-login crossmacro || true
        adduser crossmacro crossmacro 2>/dev/null || true
    fi
    
    # Ensure user is in required groups
    usermod -aG input crossmacro 2>/dev/null || true
    usermod -aG uinput crossmacro 2>/dev/null || true
    usermod -aG crossmacro crossmacro 2>/dev/null || true

    # Best effort: make uinput available immediately for the daemon.
    # Persistent boot-time loading is handled by /usr/lib/modules-load.d/crossmacro.conf.
    if command -v modprobe >/dev/null 2>&1; then
        modprobe uinput >/dev/null 2>&1 || :
    fi

    # Reload udev rules so /dev/uinput permissions are applied before daemon start.
    udevadm control --reload-rules && udevadm trigger >/dev/null 2>&1 || :
    udevadm settle >/dev/null 2>&1 || :

    # Debian policy compliant systemd integration
    if [ -d /run/systemd/system ]; then
        systemctl --system daemon-reload >/dev/null || true
        deb-systemd-helper unmask crossmacro.service >/dev/null || true
        deb-systemd-helper enable crossmacro.service >/dev/null || true
        deb-systemd-invoke start crossmacro.service >/dev/null || true
    fi

    echo "CrossMacro Daemon installed and started."
    echo "NOTE: Add your user to 'crossmacro' group to communicate with the daemon:"
    if [ -n "\${SUDO_USER:-}" ]; then
        echo "      sudo usermod -aG crossmacro \$SUDO_USER"
    else
        echo "      sudo usermod -aG crossmacro <your-username>"
    fi
fi
EOF
chmod 755 "$DEB_DIR/DEBIAN/postinst"

# Create prerm script
cat > "$DEB_DIR/DEBIAN/prerm" << EOF
#!/bin/bash
set -e

if [ "\$1" = "remove" ]; then
    if [ -d /run/systemd/system ]; then
        deb-systemd-invoke stop crossmacro.service >/dev/null || true
    fi
fi
EOF
chmod 755 "$DEB_DIR/DEBIAN/prerm"

# Create postrm script (cleanup after removal/upgrade)
cat > "$DEB_DIR/DEBIAN/postrm" << EOF
#!/bin/bash
set -e

if [ "\$1" = "remove" ]; then
    if [ -d /run/systemd/system ]; then
        systemctl --system daemon-reload >/dev/null || true
    fi
fi

if [ "\$1" = "purge" ]; then
    if [ -d /run/systemd/system ]; then
        deb-systemd-helper purge crossmacro.service >/dev/null || true
        deb-systemd-helper unmask crossmacro.service >/dev/null || true
        systemctl --system daemon-reload >/dev/null || true
    fi
fi
EOF
chmod 755 "$DEB_DIR/DEBIAN/postrm"

# 4. Copy Files
echo "Copying UI files..."
# Copy binaries to /usr/lib/crossmacro
cp -r "$PUBLISH_DIR/"* "$DEB_DIR/usr/lib/$APP_NAME/"
PACKAGED_UI_BINARY="$DEB_DIR/usr/lib/$APP_NAME/CrossMacro.UI"
verify_binary_arch "$PACKAGED_UI_BINARY" "$TARGET_ARCH_RESOLVED"

# Patch UI binary for non-NixOS systems
if command -v patchelf >/dev/null; then
    if [ -n "$ELF_INTERPRETER" ]; then
        echo "Patching UI binary interpreter: $ELF_INTERPRETER"
        patchelf --set-interpreter "$ELF_INTERPRETER" "$DEB_DIR/usr/lib/$APP_NAME/CrossMacro.UI"
    else
        echo "Warning: No known glibc interpreter for target '$TARGET_ARCH_RESOLVED'; skipping patchelf."
    fi
fi

# Build and Copy Daemon
echo "Copying Daemon files..."
mkdir -p "$DEB_DIR/usr/lib/$APP_NAME/daemon"

# If DAEMON_DIR is provided, use pre-built daemon; otherwise build it
if [ -n "${DAEMON_DIR:-}" ] && [ -d "${DAEMON_DIR:-}" ]; then
    echo "Using pre-built daemon from: $DAEMON_DIR"
    cp -r "$DAEMON_DIR/"* "$DEB_DIR/usr/lib/$APP_NAME/daemon/"
else
    echo "Building Daemon (DAEMON_DIR not set)..."
    dotnet publish ../src/CrossMacro.Daemon/CrossMacro.Daemon.csproj \
        -c Release \
        -r "$DAEMON_RID" \
        -p:Version=$VERSION \
        -o "$DEB_DIR/usr/lib/$APP_NAME/daemon"
fi
PACKAGED_DAEMON_BINARY="$DEB_DIR/usr/lib/$APP_NAME/daemon/CrossMacro.Daemon"
verify_binary_arch "$PACKAGED_DAEMON_BINARY" "$TARGET_ARCH_RESOLVED"

# Patch Daemon binary for non-NixOS systems
if command -v patchelf >/dev/null; then
    if [ -n "$ELF_INTERPRETER" ]; then
        echo "Patching Daemon binary interpreter: $ELF_INTERPRETER"
        patchelf --set-interpreter "$ELF_INTERPRETER" "$DEB_DIR/usr/lib/$APP_NAME/daemon/CrossMacro.Daemon"
    else
        echo "Warning: No known glibc interpreter for target '$TARGET_ARCH_RESOLVED'; skipping patchelf."
    fi
fi

# Ensure binaries have executable permissions
chmod +x "$DEB_DIR/usr/lib/$APP_NAME/CrossMacro.UI"
chmod +x "$DEB_DIR/usr/lib/$APP_NAME/daemon/CrossMacro.Daemon"
# Cleanup unnecessary files if any (pdb etc) - though StripSymbols should handle it.
# With AOT, the output is the executable. We might get a .dbg file if not stripped, but we set StripSymbols.

# Copy Service File to /usr/lib/systemd/system (FHS compliant)
echo "Configuring Service..."
mkdir -p "$DEB_DIR/usr/lib/systemd/system"
cp "daemon/crossmacro.service" "$DEB_DIR/usr/lib/systemd/system/crossmacro.service"

# Copy Polkit Policy
echo "Copying Polkit Policy..."
mkdir -p "$DEB_DIR/usr/share/polkit-1/actions"
cp "assets/io.github.alper_han.crossmacro.policy" "$DEB_DIR/usr/share/polkit-1/actions/io.github.alper_han.crossmacro.policy"

# Copy Polkit Rules
echo "Copying Polkit Rules..."
mkdir -p "$DEB_DIR/usr/share/polkit-1/rules.d"
cp "assets/50-crossmacro.rules" "$DEB_DIR/usr/share/polkit-1/rules.d/50-crossmacro.rules"

# Copy udev rules
echo "Copying udev rules..."
cp "assets/99-crossmacro.rules" "$DEB_DIR/usr/lib/udev/rules.d/99-crossmacro.rules"

# Copy modules-load config
echo "Copying modules-load config..."
mkdir -p "$DEB_DIR/usr/lib/modules-load.d"
cp "assets/crossmacro-modules.conf" "$DEB_DIR/usr/lib/modules-load.d/crossmacro.conf"

# Create symlink in /usr/bin
ln -s "/usr/lib/$APP_NAME/CrossMacro.UI" "$DEB_DIR/usr/bin/$APP_NAME"

# Copy Icon
# Install Icons
echo "Installing icons..."
cp -r "../src/CrossMacro.UI/Assets/icons/"* "$DEB_DIR/usr/share/icons/hicolor/"

# Copy Desktop File
cp "assets/CrossMacro.desktop" "$DEB_DIR/usr/share/applications/$APP_NAME.desktop"

# Install man page (Debian policy: compressed under /usr/share/man/man1)
echo "Installing man page..."
if [ ! -f "$MANPAGE_SOURCE" ]; then
    echo "Error: man page source not found: $MANPAGE_SOURCE"
    exit 1
fi
gzip -n -9 -c "$MANPAGE_SOURCE" > "$DEB_DIR/usr/share/man/man1/crossmacro.1.gz"

# 5. Build DEB Package
echo "Building DEB package..."
if command -v dpkg-deb &> /dev/null; then
    dpkg-deb --build "$DEB_DIR" "$OUTPUT_DEB"
    echo "DEB package created: $OUTPUT_DEB (tag version: $PACKAGE_VERSION)"
else
    echo "Error: dpkg-deb not found. Cannot build .deb package."
    echo "The directory structure is ready in '$DEB_DIR'."
fi
