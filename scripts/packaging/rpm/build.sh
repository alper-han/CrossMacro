#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPTS_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
# shellcheck source=scripts/lib/version.sh
source "$SCRIPTS_DIR/lib/version.sh"
# shellcheck source=scripts/lib/platform.sh
source "$SCRIPTS_DIR/lib/platform.sh"
# shellcheck source=scripts/packaging/rpm/common.sh
source "$SCRIPT_DIR/common.sh"

PROJECT_ROOT="$(cd "$SCRIPTS_DIR/.." && pwd)"

# Configuration
VERSION="$(get_version)"
RPM_VERSION="$(to_rpm_version)"
RPM_RELEASE="$(to_rpm_release)"
PACKAGE_VERSION="$(to_filename_version)"
TARGET_ARCH_RESOLVED="$(get_target_arch)"
EXPECTED_RPM_ARCH="$(to_rpm_arch "$TARGET_ARCH_RESOLVED")"
RPM_ARCH="${RPM_ARCH:-$EXPECTED_RPM_ARCH}"
if [ "$RPM_ARCH" != "$EXPECTED_RPM_ARCH" ]; then
    echo "Error: RPM_ARCH '$RPM_ARCH' does not match target architecture '$TARGET_ARCH_RESOLVED' (expected '$EXPECTED_RPM_ARCH')." >&2
    exit 1
fi
DOTNET_ARCH="$(to_dotnet_arch "$TARGET_ARCH_RESOLVED")"
DAEMON_RID="linux-$DOTNET_ARCH"
ELF_INTERPRETER="${ELF_INTERPRETER:-$(get_glibc_interpreter "$TARGET_ARCH_RESOLVED")}"
PUBLISH_DIR="${PUBLISH_DIR:-$SCRIPTS_DIR/../publish}"  # Use env var or default to repository publish input
# An explicitly supplied artifact must never silently trigger another publish.
if [ -n "${DAEMON_DIR:-}" ] && [ ! -f "$DAEMON_DIR/CrossMacro.Daemon" ]; then
    echo "Error: DAEMON_DIR does not contain CrossMacro.Daemon: $DAEMON_DIR" >&2
    exit 1
fi
ARTIFACT_ROOT="${CROSSMACRO_ARTIFACT_ROOT:-$PROJECT_ROOT/artifacts}"
RPM_OUTPUT_DIR="${RPM_OUTPUT_DIR:-$ARTIFACT_ROOT/packages/rpm}"
RPM_BUILD_DIR="${RPM_BUILD_DIR:-$ARTIFACT_ROOT/work/rpm}"
assert_safe_linux_work_dir "$RPM_BUILD_DIR" "$PROJECT_ROOT" "$PUBLISH_DIR" "${DAEMON_DIR:-}"


mkdir -p "$RPM_OUTPUT_DIR"

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

stage_parent="$RPM_BUILD_DIR/stage"
stage_root="$stage_parent/crossmacro-$RPM_VERSION"
mkdir -p "$stage_root/publish" "$stage_root/daemon"

echo "Copying assets..."
cp -a "$PUBLISH_DIR/." "$stage_root/publish/"
PACKAGED_UI_BINARY="$stage_root/publish/CrossMacro.UI"
verify_binary_arch "$PACKAGED_UI_BINARY" "$TARGET_ARCH_RESOLVED"

# Patch UI binary for non-NixOS systems
if command -v patchelf >/dev/null; then
    if [ -n "$ELF_INTERPRETER" ]; then
        echo "Patching UI binary interpreter: $ELF_INTERPRETER"
        patchelf --set-interpreter "$ELF_INTERPRETER" "$PACKAGED_UI_BINARY"
    else
        echo "Warning: No known glibc interpreter for target '$TARGET_ARCH_RESOLVED'; skipping patchelf."
    fi
fi

# GitHub Actions artifacts normalize file permissions to 0644 on download.
# Restore execute bits before packaging so installed RPM binaries remain runnable.
chmod +x "$PACKAGED_UI_BINARY"

# Build and Copy Daemon
echo "Copying Daemon files..."


# If DAEMON_DIR is provided, use pre-built daemon; otherwise build it
if [ -n "${DAEMON_DIR:-}" ]; then
    echo "Using pre-built daemon from: $DAEMON_DIR"
    cp -r "$DAEMON_DIR/"* "$stage_root/daemon/"
else
    echo "Building Daemon (DAEMON_DIR not set)..."
    dotnet publish "$PROJECT_ROOT/src/CrossMacro.Daemon/CrossMacro.Daemon.csproj" \
        -c Release \
        -r "$DAEMON_RID" \
        -p:CrossMacroPublishProfile=native-aot \
        -p:Version="$VERSION" \
        -o "$stage_root/daemon"
fi
PACKAGED_DAEMON_BINARY="$stage_root/daemon/CrossMacro.Daemon"
verify_binary_arch "$PACKAGED_DAEMON_BINARY" "$TARGET_ARCH_RESOLVED"

# Patch Daemon binary for non-NixOS systems
if command -v patchelf >/dev/null; then
    if [ -n "$ELF_INTERPRETER" ]; then
        echo "Patching Daemon binary interpreter: $ELF_INTERPRETER"
        patchelf --set-interpreter "$ELF_INTERPRETER" "$PACKAGED_DAEMON_BINARY"
    else
        echo "Warning: No known glibc interpreter for target '$TARGET_ARCH_RESOLVED'; skipping patchelf."
    fi
fi

chmod +x "$PACKAGED_DAEMON_BINARY"

for relative in "${RPM_ASSET_PATHS[@]}"; do
    mkdir -p -- "$stage_root/$(dirname -- "$relative")"
    cp -a -- "$PROJECT_ROOT/$relative" "$stage_root/$relative"
done
render_rpm_spec "$SCRIPT_DIR/crossmacro.spec" "$RPM_BUILD_DIR/SPECS/crossmacro.spec" "$RPM_VERSION" "$RPM_RELEASE"
write_rpm_source0 "$stage_parent" "$RPM_VERSION" "$RPM_BUILD_DIR/SOURCES"

# 4. Build RPM
echo "Building RPM package..."
if command -v rpmbuild &> /dev/null; then
    rpmbuild --define "_topdir $RPM_BUILD_DIR" \
             --define "_target_cpu $RPM_ARCH" \
             --with prebuilt --nodeps \
             -bb "$RPM_BUILD_DIR/SPECS/crossmacro.spec"
    
    # Copy the package into the shared release artifact directory.
    cp "$RPM_BUILD_DIR"/RPMS/"$RPM_ARCH"/*.rpm "$RPM_OUTPUT_DIR/"
    echo "RPM package created for version: $PACKAGE_VERSION"
else
    echo "Error: rpmbuild not found. Cannot build .rpm package."
    echo "The directory structure is ready in '$RPM_BUILD_DIR'."
    exit 1
fi
