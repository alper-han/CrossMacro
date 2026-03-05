#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/lib/version.sh
source "$SCRIPT_DIR/lib/version.sh"
# shellcheck source=scripts/lib/platform.sh
source "$SCRIPT_DIR/lib/platform.sh"

APP_ID="io.github.alper_han.crossmacro"
VERSION="$(get_version)"
PACKAGE_VERSION="$(to_filename_version)"
TARGET_ARCH_RESOLVED="$(get_target_arch)"
FLATPAK_ARCH="${FLATPAK_ARCH:-$(to_flatpak_arch "$TARGET_ARCH_RESOLVED")}"
ELF_INTERPRETER="${ELF_INTERPRETER:-$(get_glibc_interpreter "$TARGET_ARCH_RESOLVED")}"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
FLATPAK_DIR="$PROJECT_ROOT/flatpak"
BUILD_DIR="$SCRIPT_DIR/flatpak-source"
PUBLISH_DIR="${PUBLISH_DIR:-$PROJECT_ROOT/publish}"
OUTPUT_BUNDLE="$APP_ID-$PACKAGE_VERSION-$FLATPAK_ARCH.flatpak"

echo "=== CrossMacro Flatpak Builder ==="
echo "Version: $PACKAGE_VERSION"
echo "App ID: $APP_ID"
echo "Target architecture: $TARGET_ARCH_RESOLVED (Flatpak: $FLATPAK_ARCH)"

# Verify publish directory
if [ ! -d "$PUBLISH_DIR" ]; then
    echo "Error: Publish directory not found: $PUBLISH_DIR"
    echo "Build first with:"
    echo "  dotnet publish src/CrossMacro.UI.Linux/CrossMacro.UI.Linux.csproj -c Release -r linux-x64 --self-contained -o publish"
    exit 1
fi

echo "Using binaries from: $PUBLISH_DIR"

# Clean previous build
rm -rf "$BUILD_DIR" "$FLATPAK_DIR/crossmacro-flatpak-source.tar.gz"
mkdir -p "$BUILD_DIR"

# Copy binaries
echo "Copying binaries..."
cp -r "$PUBLISH_DIR"/* "$BUILD_DIR/"

# Patch interpreter for non-NixOS systems (Flatpak uses standard glibc)
echo "Patching ELF binaries..."
if command -v patchelf &> /dev/null; then
    if [ -n "$ELF_INTERPRETER" ]; then
        for elf in "$BUILD_DIR"/CrossMacro.UI "$BUILD_DIR"/createdump $(find "$BUILD_DIR" -name "*.so" 2>/dev/null); do
            if [ -f "$elf" ] && file "$elf" | grep -q "ELF"; then
                patchelf --set-interpreter "$ELF_INTERPRETER" "$elf" 2>/dev/null || true
            fi
        done
    else
        echo "Warning: No known glibc interpreter for target '$TARGET_ARCH_RESOLVED'; skipping patchelf."
    fi
fi

# Copy icons
echo "Copying icons..."
mkdir -p "$BUILD_DIR/icons"
cp -r "$PROJECT_ROOT/src/CrossMacro.UI/Assets/icons/"* "$BUILD_DIR/icons/"

# Copy desktop entry and metainfo
echo "Copying desktop files..."
cp "$FLATPAK_DIR/$APP_ID.desktop" "$BUILD_DIR/"
cp "$FLATPAK_DIR/$APP_ID.metainfo.xml" "$BUILD_DIR/"
cp "$FLATPAK_DIR/crossmacro.sh" "$BUILD_DIR/"
cp "$PROJECT_ROOT/LICENSE" "$BUILD_DIR/"

# Build Flatpak (dir source, no archive needed)

# Build Flatpak
echo "=== Building Flatpak ==="
cd "$FLATPAK_DIR"

# Check for flatpak-builder
if ! command -v flatpak-builder &> /dev/null; then
    echo "Error: flatpak-builder not found."
    echo "Install with: sudo apt install flatpak-builder"
    exit 1
fi

# Build
flatpak-builder --force-clean --user \
    --arch="$FLATPAK_ARCH" \
    --install-deps-from=flathub \
    --disable-updates \
    build-dir "$APP_ID.yml"

# Create repo and bundle
echo "Creating Flatpak bundle..."
flatpak-builder --repo=repo --force-clean --disable-updates --arch="$FLATPAK_ARCH" build-dir "$APP_ID.yml"
flatpak build-bundle --arch="$FLATPAK_ARCH" repo "$OUTPUT_BUNDLE" "$APP_ID"

# Cleanup
rm -rf build-dir repo "$BUILD_DIR" crossmacro-flatpak-source.tar.gz

echo ""
echo "=== Build Complete ==="
echo "Output: $FLATPAK_DIR/$OUTPUT_BUNDLE"
echo ""
echo "To install locally:"
echo "  flatpak --user install $FLATPAK_DIR/$OUTPUT_BUNDLE"
echo ""
echo "To run:"
echo "  flatpak run $APP_ID"
