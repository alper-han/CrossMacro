#!/usr/bin/env bash
set -e

# Configuration
APP_NAME="crossmacro"
VERSION="${VERSION="0.9.5"}"
PUBLISH_DIR="${PUBLISH_DIR:-../publish}"  # Use env var or default to ../publish
RPM_BUILD_DIR="rpm_build"
ICON_PATH="../src/CrossMacro.UI/Assets/mouse-icon.png"

# Clean previous build
rm -rf "$RPM_BUILD_DIR"

# Verify publish directory exists
if [ ! -d "$PUBLISH_DIR" ]; then
    echo "Error: Publish directory not found: $PUBLISH_DIR"
    echo "Please build the application first or set PUBLISH_DIR environment variable"
    exit 1
fi

echo "Using pre-built binaries from: $PUBLISH_DIR"

# 1. Prepare RPM Build Directory
echo "Preparing RPM build directory..."
mkdir -p "$RPM_BUILD_DIR"/{BUILD,RPMS,SOURCES,SPECS,SRPMS}

# 2. Copy Assets to SOURCES
echo "Copying assets..."
cp -r "$PUBLISH_DIR" "$RPM_BUILD_DIR/SOURCES/publish"

# Patch UI binary for non-NixOS systems
if command -v patchelf >/dev/null; then
    echo "Patching UI binary interpreter..."
    patchelf --set-interpreter /lib64/ld-linux-x86-64.so.2 "$RPM_BUILD_DIR/SOURCES/publish/CrossMacro.UI"
fi

# Build and Copy Daemon
echo "Copying Daemon files..."
mkdir -p "$RPM_BUILD_DIR/SOURCES/daemon"

# If DAEMON_DIR is provided, use pre-built daemon; otherwise build it
if [ -n "$DAEMON_DIR" ] && [ -d "$DAEMON_DIR" ]; then
    echo "Using pre-built daemon from: $DAEMON_DIR"
    cp -r "$DAEMON_DIR/"* "$RPM_BUILD_DIR/SOURCES/daemon/"
else
    echo "Building Daemon (DAEMON_DIR not set)..."
    dotnet publish ../src/CrossMacro.Daemon/CrossMacro.Daemon.csproj -c Release -p:Version=$VERSION -o "$RPM_BUILD_DIR/SOURCES/daemon"
fi

# Patch Daemon binary for non-NixOS systems
if command -v patchelf >/dev/null; then
    echo "Patching Daemon binary interpreter..."
    patchelf --set-interpreter /lib64/ld-linux-x86-64.so.2 "$RPM_BUILD_DIR/SOURCES/daemon/CrossMacro.Daemon"
fi

cp "$ICON_PATH" "$RPM_BUILD_DIR/SOURCES/crossmacro.png"
cp "assets/CrossMacro.desktop" "$RPM_BUILD_DIR/SOURCES/CrossMacro.desktop"
cp "daemon/crossmacro.service" "$RPM_BUILD_DIR/SOURCES/crossmacro.service"
cp "assets/99-crossmacro.rules" "$RPM_BUILD_DIR/SOURCES/99-crossmacro.rules"
cp "packaging/rpm/crossmacro.te" "$RPM_BUILD_DIR/SOURCES/crossmacro.te"
cp "assets/org.crossmacro.policy" "$RPM_BUILD_DIR/SOURCES/org.crossmacro.policy"
cp "assets/50-crossmacro.rules" "$RPM_BUILD_DIR/SOURCES/50-crossmacro.rules"
cp "assets/crossmacro-modules.conf" "$RPM_BUILD_DIR/SOURCES/crossmacro-modules.conf"

# Copy Icons to SOURCES
mkdir -p "$RPM_BUILD_DIR/SOURCES/icons"
cp -r "../src/CrossMacro.UI/Assets/icons/"* "$RPM_BUILD_DIR/SOURCES/icons/"

# 3. Copy Spec File
cp "packaging/rpm/crossmacro.spec" "$RPM_BUILD_DIR/SPECS/"

# 4. Build RPM
echo "Building RPM package..."
if command -v rpmbuild &> /dev/null; then
    rpmbuild --define "_topdir $(pwd)/$RPM_BUILD_DIR" \
             --define "_sourcedir $(pwd)/$RPM_BUILD_DIR/SOURCES" \
             --define "version $VERSION" \
             --nodeps \
             -bb "$RPM_BUILD_DIR/SPECS/crossmacro.spec"
    
    # Copy RPM to scripts directory for GitHub release
    cp "$RPM_BUILD_DIR"/RPMS/x86_64/*.rpm .
    echo "RPM package created: $(ls *.rpm)"
else
    echo "Error: rpmbuild not found. Cannot build .rpm package."
    echo "The directory structure is ready in '$RPM_BUILD_DIR'."
    exit 1
fi
