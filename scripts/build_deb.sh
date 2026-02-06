#!/usr/bin/env bash
set -e

# Configuration
APP_NAME="crossmacro"
VERSION="${VERSION="0.9.4"}"
ARCH="amd64"
PUBLISH_DIR="${PUBLISH_DIR:-../publish}"  # Use env var or default to ../publish
DEB_DIR="deb_package"
ICON_PATH="../src/CrossMacro.UI/Assets/mouse-icon.png"

# Clean previous build
rm -rf "$DEB_DIR" "${APP_NAME}-${VERSION}_${ARCH}.deb"

# Verify publish directory exists
if [ ! -d "$PUBLISH_DIR" ]; then
    echo "Error: Publish directory not found: $PUBLISH_DIR"
    echo "Please build the application first or set PUBLISH_DIR environment variable"
    exit 1
fi

echo "Using pre-built binaries from: $PUBLISH_DIR"

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


# 3. Create Control File
echo "Creating control file..."
cat > "$DEB_DIR/DEBIAN/control" << EOF
Package: $APP_NAME
Version: $VERSION
Section: utils
Priority: optional
Architecture: $ARCH
Depends: libc6, libstdc++6, polkitd | policykit-1, libxtst6, zlib1g, libssl3 | libssl1.1, libsystemd0
Recommends: libx11-6, libice6, libsm6, libfontconfig1
Maintainer: Zynix <crossmacro@zynix.net>
Description: Mouse and Keyboard Macro Automation Tool
 A powerful cross-platform mouse and keyboard macro automation tool.
 Supports text expansion and works on Linux (Wayland/X11) and Windows.
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

    # Create user if not exists
    if ! getent passwd crossmacro >/dev/null; then
        adduser --system --no-create-home --ingroup input --disabled-login crossmacro || true
        adduser crossmacro crossmacro 2>/dev/null || true
    fi
    
    # Ensure user is in input group and crossmacro group
    usermod -aG input crossmacro 2>/dev/null || true
    usermod -aG crossmacro crossmacro 2>/dev/null || true

    # Debian policy compliant systemd integration
    if [ -d /run/systemd/system ]; then
        systemctl --system daemon-reload >/dev/null || true
        deb-systemd-helper unmask crossmacro.service >/dev/null || true
        deb-systemd-helper enable crossmacro.service >/dev/null || true
        deb-systemd-invoke start crossmacro.service >/dev/null || true
    fi
    
    # Reload udev rules
    udevadm control --reload-rules && udevadm trigger >/dev/null 2>&1 || :
    
    echo "CrossMacro Daemon installed and started."
    echo "NOTE: Add your user to 'crossmacro' group to communicate with the daemon:"
    echo "      sudo usermod -aG crossmacro \$SUDO_USER"
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

# Patch UI binary for non-NixOS systems
if command -v patchelf >/dev/null; then
    echo "Patching UI binary interpreter..."
    patchelf --set-interpreter /lib64/ld-linux-x86-64.so.2 "$DEB_DIR/usr/lib/$APP_NAME/CrossMacro.UI"
fi

# Build and Copy Daemon
echo "Copying Daemon files..."
mkdir -p "$DEB_DIR/usr/lib/$APP_NAME/daemon"

# If DAEMON_DIR is provided, use pre-built daemon; otherwise build it
if [ -n "$DAEMON_DIR" ] && [ -d "$DAEMON_DIR" ]; then
    echo "Using pre-built daemon from: $DAEMON_DIR"
    cp -r "$DAEMON_DIR/"* "$DEB_DIR/usr/lib/$APP_NAME/daemon/"
else
    echo "Building Daemon (DAEMON_DIR not set)..."
    dotnet publish ../src/CrossMacro.Daemon/CrossMacro.Daemon.csproj -c Release -p:Version=$VERSION -o "$DEB_DIR/usr/lib/$APP_NAME/daemon"
fi

# Patch Daemon binary for non-NixOS systems
if command -v patchelf >/dev/null; then
    echo "Patching Daemon binary interpreter..."
    patchelf --set-interpreter /lib64/ld-linux-x86-64.so.2 "$DEB_DIR/usr/lib/$APP_NAME/daemon/CrossMacro.Daemon"
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
cp "assets/org.crossmacro.policy" "$DEB_DIR/usr/share/polkit-1/actions/org.crossmacro.policy"

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

# 5. Build DEB Package
echo "Building DEB package..."
if command -v dpkg-deb &> /dev/null; then
    dpkg-deb --build "$DEB_DIR" "${APP_NAME}-${VERSION}_${ARCH}.deb"
    echo "DEB package created: ${APP_NAME}-${VERSION}_${ARCH}.deb"
else
    echo "Error: dpkg-deb not found. Cannot build .deb package."
    echo "The directory structure is ready in '$DEB_DIR'."
fi
