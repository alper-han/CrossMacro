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
RPM_VERSION="$(to_rpm_version)"
RPM_RELEASE="$(to_rpm_release)"
PACKAGE_VERSION="$(to_filename_version)"
TARGET_ARCH_RESOLVED="$(get_target_arch)"
RPM_ARCH="${RPM_ARCH:-$(to_rpm_arch "$TARGET_ARCH_RESOLVED")}"
DOTNET_ARCH="$(to_dotnet_arch "$TARGET_ARCH_RESOLVED")"
DAEMON_RID="linux-$DOTNET_ARCH"
ELF_INTERPRETER="${ELF_INTERPRETER:-$(get_glibc_interpreter "$TARGET_ARCH_RESOLVED")}"
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

UI_SOURCE_BINARY="$PUBLISH_DIR/CrossMacro.UI"
if [ ! -f "$UI_SOURCE_BINARY" ]; then
    echo "Error: UI binary not found in publish directory: $UI_SOURCE_BINARY"
    exit 1
fi
verify_binary_arch "$UI_SOURCE_BINARY" "$TARGET_ARCH_RESOLVED"

echo "Using pre-built binaries from: $PUBLISH_DIR"
echo "Packaging architecture: $RPM_ARCH (target: $TARGET_ARCH_RESOLVED)"
echo "Daemon publish RID: $DAEMON_RID"

# 1. Prepare RPM Build Directory
echo "Preparing RPM build directory..."
mkdir -p "$RPM_BUILD_DIR"/{BUILD,RPMS,SOURCES,SPECS,SRPMS}

# 2. Copy Assets to SOURCES
echo "Copying assets..."
cp -r "$PUBLISH_DIR" "$RPM_BUILD_DIR/SOURCES/publish"
PACKAGED_UI_BINARY="$RPM_BUILD_DIR/SOURCES/publish/CrossMacro.UI"
verify_binary_arch "$PACKAGED_UI_BINARY" "$TARGET_ARCH_RESOLVED"

# Patch UI binary for non-NixOS systems
if command -v patchelf >/dev/null; then
    if [ -n "$ELF_INTERPRETER" ]; then
        echo "Patching UI binary interpreter: $ELF_INTERPRETER"
        patchelf --set-interpreter "$ELF_INTERPRETER" "$RPM_BUILD_DIR/SOURCES/publish/CrossMacro.UI"
    else
        echo "Warning: No known glibc interpreter for target '$TARGET_ARCH_RESOLVED'; skipping patchelf."
    fi
fi

# Build and Copy Daemon
echo "Copying Daemon files..."
mkdir -p "$RPM_BUILD_DIR/SOURCES/daemon"

# If DAEMON_DIR is provided, use pre-built daemon; otherwise build it
if [ -n "${DAEMON_DIR:-}" ] && [ -d "${DAEMON_DIR:-}" ]; then
    echo "Using pre-built daemon from: $DAEMON_DIR"
    cp -r "$DAEMON_DIR/"* "$RPM_BUILD_DIR/SOURCES/daemon/"
else
    echo "Building Daemon (DAEMON_DIR not set)..."
    dotnet publish ../src/CrossMacro.Daemon/CrossMacro.Daemon.csproj \
        -c Release \
        -r "$DAEMON_RID" \
        -p:Version=$VERSION \
        -o "$RPM_BUILD_DIR/SOURCES/daemon"
fi
PACKAGED_DAEMON_BINARY="$RPM_BUILD_DIR/SOURCES/daemon/CrossMacro.Daemon"
verify_binary_arch "$PACKAGED_DAEMON_BINARY" "$TARGET_ARCH_RESOLVED"

# Patch Daemon binary for non-NixOS systems
if command -v patchelf >/dev/null; then
    if [ -n "$ELF_INTERPRETER" ]; then
        echo "Patching Daemon binary interpreter: $ELF_INTERPRETER"
        patchelf --set-interpreter "$ELF_INTERPRETER" "$RPM_BUILD_DIR/SOURCES/daemon/CrossMacro.Daemon"
    else
        echo "Warning: No known glibc interpreter for target '$TARGET_ARCH_RESOLVED'; skipping patchelf."
    fi
fi

cp "$ICON_PATH" "$RPM_BUILD_DIR/SOURCES/crossmacro.png"
cp "assets/CrossMacro.desktop" "$RPM_BUILD_DIR/SOURCES/CrossMacro.desktop"
cp "daemon/crossmacro.service" "$RPM_BUILD_DIR/SOURCES/crossmacro.service"
cp "assets/99-crossmacro.rules" "$RPM_BUILD_DIR/SOURCES/99-crossmacro.rules"
cp "packaging/rpm/crossmacro.te" "$RPM_BUILD_DIR/SOURCES/crossmacro.te"
cp "assets/io.github.alper_han.crossmacro.policy" "$RPM_BUILD_DIR/SOURCES/io.github.alper_han.crossmacro.policy"
cp "assets/50-crossmacro.rules" "$RPM_BUILD_DIR/SOURCES/50-crossmacro.rules"
cp "assets/crossmacro-modules.conf" "$RPM_BUILD_DIR/SOURCES/crossmacro-modules.conf"
cp "../docs/man/crossmacro.1" "$RPM_BUILD_DIR/SOURCES/crossmacro.1"

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
             --define "_target_cpu $RPM_ARCH" \
             --define "version $RPM_VERSION" \
             --define "release $RPM_RELEASE" \
             --nodeps \
             -bb "$RPM_BUILD_DIR/SPECS/crossmacro.spec"
    
    # Copy RPM to scripts directory for GitHub release
    cp "$RPM_BUILD_DIR"/RPMS/"$RPM_ARCH"/*.rpm .
    echo "RPM package created for version: $PACKAGE_VERSION"
else
    echo "Error: rpmbuild not found. Cannot build .rpm package."
    echo "The directory structure is ready in '$RPM_BUILD_DIR'."
    exit 1
fi
