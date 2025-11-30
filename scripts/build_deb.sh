#!/bin/bash
set -e

# Configuration
APP_NAME="crossmacro"
VERSION="${VERSION:-1.0.0}"
ARCH="amd64"
PUBLISH_DIR="${PUBLISH_DIR:-../publish}"  # Use env var or default to ../publish
DEB_DIR="deb_package"
ICON_PATH="../src/CrossMacro.UI/Assets/mouse-icon.png"

# Clean previous build
rm -rf "$DEB_DIR" "${APP_NAME}_${VERSION}_${ARCH}.deb"

# Verify publish directory exists
if [ ! -d "$PUBLISH_DIR" ]; then
    echo "Error: Publish directory not found: $PUBLISH_DIR"
    echo "Please build the application first or set PUBLISH_DIR environment variable"
    exit 1
fi

echo "Using pre-built binaries from: $PUBLISH_DIR"

# 1. Create Directory Structure
echo "Creating directory structure..."
mkdir -p "$DEB_DIR/DEBIAN"
mkdir -p "$DEB_DIR/usr/bin"
mkdir -p "$DEB_DIR/usr/lib/$APP_NAME"
mkdir -p "$DEB_DIR/usr/share/applications"
mkdir -p "$DEB_DIR/usr/share/icons/hicolor/256x256/apps"

# 2. Create Control File
echo "Creating control file..."
cat > "$DEB_DIR/DEBIAN/control" << EOF
Package: $APP_NAME
Version: $VERSION
Section: utils
Priority: optional
Architecture: $ARCH
Maintainer: Zynix <crossmacro@zynix.net>
Description: Mouse Macro Automation Tool for Linux
 A powerful mouse macro automation tool for Linux Wayland compositors.
 Supports Hyprland, KDE Plasma, and GNOME Shell.
EOF

# 3. Copy Files
echo "Copying files..."
# Copy binaries to /usr/lib/crossmacro
cp -r "$PUBLISH_DIR/"* "$DEB_DIR/usr/lib/$APP_NAME/"

# Create symlink in /usr/bin
ln -s "/usr/lib/$APP_NAME/CrossMacro.UI" "$DEB_DIR/usr/bin/$APP_NAME"

# Copy Icon
cp "$ICON_PATH" "$DEB_DIR/usr/share/icons/hicolor/256x256/apps/$APP_NAME.png"

# Copy Desktop File
cp "assets/CrossMacro.desktop" "$DEB_DIR/usr/share/applications/$APP_NAME.desktop"

# 4. Build DEB Package
echo "Building DEB package..."
if command -v dpkg-deb &> /dev/null; then
    dpkg-deb --build "$DEB_DIR" "${APP_NAME}_${VERSION}_${ARCH}.deb"
    echo "DEB package created: ${APP_NAME}_${VERSION}_${ARCH}.deb"
else
    echo "Error: dpkg-deb not found. Cannot build .deb package."
    echo "The directory structure is ready in '$DEB_DIR'."
fi
