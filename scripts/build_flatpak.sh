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
OUTPUT_BUNDLE="$APP_ID-$PACKAGE_VERSION-$FLATPAK_ARCH.flatpak"

echo "=== CrossMacro Flatpak Builder ==="
echo "Version: $PACKAGE_VERSION"
echo "App ID: $APP_ID"
echo "Target architecture: $TARGET_ARCH_RESOLVED (Flatpak: $FLATPAK_ARCH)"

# Clean previous build
rm -rf "$BUILD_DIR" "$FLATPAK_DIR/crossmacro-flatpak-source.tar.gz"
mkdir -p "$BUILD_DIR"

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
