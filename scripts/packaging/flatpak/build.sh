#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPTS_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
# shellcheck source=scripts/lib/version.sh
source "$SCRIPTS_DIR/lib/version.sh"
# shellcheck source=scripts/lib/platform.sh
source "$SCRIPTS_DIR/lib/platform.sh"

APP_ID="io.github.alper_han.crossmacro"
VERSION="$(get_version)"
PACKAGE_VERSION="$(to_filename_version)"
TARGET_ARCH_RESOLVED="$(get_target_arch)"
FLATPAK_ARCH="${FLATPAK_ARCH:-$(to_flatpak_arch "$TARGET_ARCH_RESOLVED")}"
ELF_INTERPRETER="${ELF_INTERPRETER:-$(get_glibc_interpreter "$TARGET_ARCH_RESOLVED")}"
PROJECT_ROOT="$(cd "$SCRIPTS_DIR/.." && pwd)"
FLATPAK_DIR="$PROJECT_ROOT/flatpak"
ARTIFACT_ROOT="${CROSSMACRO_ARTIFACT_ROOT:-$PROJECT_ROOT/artifacts}"
FLATPAK_OUTPUT_DIR="${FLATPAK_OUTPUT_DIR:-$ARTIFACT_ROOT/packages/flatpak}"
FLATPAK_WORK_DIR="${FLATPAK_WORK_DIR:-$ARTIFACT_ROOT/work/flatpak}"
BUILD_DIR="$FLATPAK_WORK_DIR/build-dir"
REPO_DIR="$FLATPAK_WORK_DIR/repo"
OUTPUT_BUNDLE="$APP_ID-$PACKAGE_VERSION-$FLATPAK_ARCH.flatpak"

echo "=== CrossMacro Flatpak Builder ==="
echo "Version: $PACKAGE_VERSION"
echo "App ID: $APP_ID"
echo "Target architecture: $TARGET_ARCH_RESOLVED (Flatpak: $FLATPAK_ARCH)"

# Clean previous build
rm -rf "$BUILD_DIR" "$REPO_DIR" "$FLATPAK_DIR/crossmacro-flatpak-source.tar.gz"
mkdir -p "$BUILD_DIR" "$FLATPAK_OUTPUT_DIR" "$FLATPAK_WORK_DIR"

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
    "$BUILD_DIR" "$APP_ID.yml"

# Create repo and bundle
echo "Creating Flatpak bundle..."
flatpak-builder --repo="$REPO_DIR" --force-clean --disable-updates --arch="$FLATPAK_ARCH" "$BUILD_DIR" "$APP_ID.yml"
flatpak build-bundle --arch="$FLATPAK_ARCH" "$REPO_DIR" "$FLATPAK_OUTPUT_DIR/$OUTPUT_BUNDLE" "$APP_ID"

# Cleanup
rm -rf "$BUILD_DIR" "$REPO_DIR" "$FLATPAK_DIR/crossmacro-flatpak-source.tar.gz"

echo ""
echo "=== Build Complete ==="
echo "Output: $FLATPAK_OUTPUT_DIR/$OUTPUT_BUNDLE"
echo ""
echo "To install locally:"
echo "  flatpak --user install $FLATPAK_OUTPUT_DIR/$OUTPUT_BUNDLE"
echo ""
echo "To run:"
echo "  flatpak run $APP_ID"
