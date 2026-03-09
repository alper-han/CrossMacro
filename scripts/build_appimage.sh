#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/lib/version.sh
source "$SCRIPT_DIR/lib/version.sh"
# shellcheck source=scripts/lib/platform.sh
source "$SCRIPT_DIR/lib/platform.sh"

APP_NAME="CrossMacro"
VERSION="$(get_version)"
PACKAGE_VERSION="$(to_filename_version)"
TARGET_ARCH_RESOLVED="$(get_target_arch)"
APPIMAGE_ARCH="${APPIMAGE_ARCH:-$(to_appimage_arch "$TARGET_ARCH_RESOLVED")}"
HOST_ARCH_RESOLVED="$(normalize_arch "$(uname -m)")"
APPIMAGETOOL_HOST_ARCH="${APPIMAGETOOL_HOST_ARCH:-$(to_appimage_arch "$HOST_ARCH_RESOLVED")}"
ELF_INTERPRETER="${ELF_INTERPRETER:-$(get_glibc_interpreter "$TARGET_ARCH_RESOLVED")}"
PUBLISH_DIR="${PUBLISH_DIR:-../publish}"
APP_DIR="AppDir"
APPIMAGETOOL_NAME="appimagetool-${APPIMAGETOOL_HOST_ARCH}.AppImage"
APPIMAGETOOL_VERSION="${APPIMAGETOOL_VERSION:-1.9.1}"
APPIMAGETOOL_RELEASE_API="https://api.github.com/repos/AppImage/appimagetool/releases/tags/$APPIMAGETOOL_VERSION"
APPIMAGETOOL_DOWNLOAD_URL="https://github.com/AppImage/appimagetool/releases/download/$APPIMAGETOOL_VERSION/$APPIMAGETOOL_NAME"
CURL_RETRY_DELAY_SECONDS="${CURL_RETRY_DELAY_SECONDS:-1}"
CURL_RETRY_MAX_TIME_SECONDS="${CURL_RETRY_MAX_TIME_SECONDS:-15}"
CURL_RETRY_ATTEMPTS="${CURL_RETRY_ATTEMPTS:-15}"
APPIMAGE_OUTPUT="CrossMacro-${PACKAGE_VERSION}-${APPIMAGE_ARCH}.AppImage"

curl_with_retry() {
    curl -fL \
        --retry "$CURL_RETRY_ATTEMPTS" \
        --retry-delay "$CURL_RETRY_DELAY_SECONDS" \
        --retry-max-time "$CURL_RETRY_MAX_TIME_SECONDS" \
        --retry-all-errors \
        "$@"
}

resolve_appimagetool_sha256() {
    if [ -n "${APPIMAGETOOL_SHA256:-}" ]; then
        echo "$APPIMAGETOOL_SHA256"
        return 0
    fi

    if ! command -v jq >/dev/null 2>&1; then
        echo "Error: APPIMAGETOOL_SHA256 is not set and 'jq' is unavailable to resolve a trusted digest from GitHub API."
        return 1
    fi

    local release_json
    if ! release_json="$(curl_with_retry -sS "$APPIMAGETOOL_RELEASE_API")"; then
        echo "Error: Failed to fetch appimagetool release metadata."
        return 1
    fi

    local digest
    digest="$(printf '%s' "$release_json" | jq -r --arg name "$APPIMAGETOOL_NAME" \
        '.assets[] | select(.name == $name) | .digest // empty' \
        | sed -n 's/^sha256:\([0-9a-fA-F]\{64\}\)$/\1/p' | head -n1)"

    if [ -z "$digest" ]; then
        echo "Error: Could not resolve SHA256 digest for $APPIMAGETOOL_NAME from GitHub API. Set APPIMAGETOOL_SHA256 manually."
        return 1
    fi

    echo "$digest"
}

calculate_sha256() {
    local file="$1"
    if command -v sha256sum >/dev/null 2>&1; then
        sha256sum "$file" | awk '{print $1}'
        return 0
    fi

    if command -v shasum >/dev/null 2>&1; then
        shasum -a 256 "$file" | awk '{print $1}'
        return 0
    fi

    echo "Error: Neither sha256sum nor shasum is available for checksum verification."
    return 1
}

verify_sha256() {
    local file="$1"
    local expected="$2"
    local actual

    actual="$(calculate_sha256 "$file")" || return 1
    if [ "$actual" != "$expected" ]; then
        echo "Error: Checksum verification failed for $file"
        echo "Expected: $expected"
        echo "Actual:   $actual"
        return 1
    fi
}

rm -rf "$APP_DIR"

if [ ! -d "$PUBLISH_DIR" ]; then
    echo "Error: Publish directory not found: $PUBLISH_DIR"
    exit 1
fi

echo "Using pre-built binaries from: $PUBLISH_DIR"
echo "Target architecture: $TARGET_ARCH_RESOLVED (AppImage: $APPIMAGE_ARCH, host tool: $APPIMAGETOOL_HOST_ARCH)"

mkdir -p "$APP_DIR/usr/bin" "$APP_DIR/usr/lib" "$APP_DIR/usr/share/icons/hicolor" \
         "$APP_DIR/usr/share/applications" "$APP_DIR/usr/share/metainfo"

cp -r "$PUBLISH_DIR/"* "$APP_DIR/usr/bin/"

matches_target_lib_arch() {
    local candidate="$1"

    # If "file" is unavailable during cross-arch packaging, fail closed to avoid bundling a wrong-arch library.
    if ! command -v file >/dev/null 2>&1; then
        if [ "$HOST_ARCH_RESOLVED" = "$APPIMAGE_ARCH" ]; then
            return 0
        fi

        return 1
    fi

    local expected_pattern=""
    case "$APPIMAGE_ARCH" in
        x86_64)
            expected_pattern="x86-64"
            ;;
        aarch64)
            expected_pattern="ARM aarch64"
            ;;
        *)
            return 1
            ;;
    esac

    file -L "$candidate" 2>/dev/null | grep -q "$expected_pattern"
}

resolve_libxtst_path() {
    local candidates=()
    local candidate
    local pkg_config_libdir
    local pkg_config_search_dirs

    case "$APPIMAGE_ARCH" in
        x86_64)
            candidates=(
                "/usr/lib/x86_64-linux-gnu/libXtst.so.6"
                "/usr/lib64/libXtst.so.6"
            )
            ;;
        aarch64)
            candidates=(
                "/usr/lib/aarch64-linux-gnu/libXtst.so.6"
                "/usr/lib64/libXtst.so.6"
            )
            ;;
    esac

    if [ -n "${LIBXTST_PATH:-}" ] && [ -f "${LIBXTST_PATH:-}" ] && matches_target_lib_arch "${LIBXTST_PATH:-}"; then
        echo "${LIBXTST_PATH:-}"
        return 0
    fi

    for candidate in "${candidates[@]}"; do
        if [ -f "$candidate" ] && matches_target_lib_arch "$candidate"; then
            echo "$candidate"
            return 0
        fi
    done

    if command -v pkg-config >/dev/null 2>&1; then
        pkg_config_libdir="$(pkg-config --variable=libdir xtst 2>/dev/null || true)"
        if [ -n "$pkg_config_libdir" ]; then
            candidate="$pkg_config_libdir/libXtst.so.6"
            if [ -f "$candidate" ] && matches_target_lib_arch "$candidate"; then
                echo "$candidate"
                return 0
            fi
        fi

        pkg_config_search_dirs="$(pkg-config --libs-only-L xtst 2>/dev/null || true)"
        for candidate in $pkg_config_search_dirs; do
            candidate="${candidate#-L}/libXtst.so.6"
            if [ -f "$candidate" ] && matches_target_lib_arch "$candidate"; then
                echo "$candidate"
                return 0
            fi
        done
    fi

    if [ -n "${LD_LIBRARY_PATH:-}" ]; then
        local dir
        IFS=':' read -ra ADDR <<< "$LD_LIBRARY_PATH"
        for dir in "${ADDR[@]}"; do
            candidate="$dir/libXtst.so.6"
            if [ -f "$candidate" ] && matches_target_lib_arch "$candidate"; then
                echo "$candidate"
                return 0
            fi
        done
    fi

    return 1
}

LIBXTST_PATH=""
if LIBXTST_PATH="$(resolve_libxtst_path)"; then
    :
fi

if [ -n "$LIBXTST_PATH" ]; then
    echo "Bundling libXtst.so.6 from: $LIBXTST_PATH"
    cp "$LIBXTST_PATH" "$APP_DIR/usr/lib/"
else
    echo "WARNING: target-compatible libXtst.so.6 not found for '$APPIMAGE_ARCH'. XTest support may be missing."
fi

if command -v patchelf >/dev/null; then
    if [ -n "$ELF_INTERPRETER" ]; then
        patchelf --set-interpreter "$ELF_INTERPRETER" "$APP_DIR/usr/bin/CrossMacro.UI"
    else
        echo "Warning: No known glibc interpreter for target '$TARGET_ARCH_RESOLVED'; skipping patchelf."
    fi
fi

cp "../src/CrossMacro.UI/Assets/icons/512x512/apps/crossmacro.png" "$APP_DIR/crossmacro.png"
cp "../src/CrossMacro.UI/Assets/icons/256x256/apps/crossmacro.png" "$APP_DIR/.DirIcon"
cp -r "../src/CrossMacro.UI/Assets/icons/"* "$APP_DIR/usr/share/icons/hicolor/"
cp "assets/$APP_NAME.desktop" "$APP_DIR/$APP_NAME.desktop"
cp "assets/$APP_NAME.desktop" "$APP_DIR/usr/share/applications/$APP_NAME.desktop"
cp "assets/io.github.alper-han.CrossMacro.metainfo.xml" "$APP_DIR/usr/share/metainfo/"

chmod +x "$APP_DIR/usr/bin/CrossMacro.UI"
ln -s "usr/bin/CrossMacro.UI" "$APP_DIR/AppRun"

APPIMAGETOOL_SHA256_RESOLVED="$(resolve_appimagetool_sha256)"

if [ ! -f "$APPIMAGETOOL_NAME" ]; then
    echo "Downloading appimagetool $APPIMAGETOOL_VERSION (retry: ${CURL_RETRY_DELAY_SECONDS}s interval, ${CURL_RETRY_MAX_TIME_SECONDS}s max window)..."
    curl_with_retry -o "$APPIMAGETOOL_NAME" "$APPIMAGETOOL_DOWNLOAD_URL"
fi

verify_sha256 "$APPIMAGETOOL_NAME" "$APPIMAGETOOL_SHA256_RESOLVED"
chmod +x "$APPIMAGETOOL_NAME"

export ARCH="$APPIMAGE_ARCH" PATH=$PWD:$PATH
export APPIMAGE_EXTRACT_AND_RUN="${APPIMAGE_EXTRACT_AND_RUN:-1}"
TOOL_CMD="./$APPIMAGETOOL_NAME"
command -v appimage-run &>/dev/null && TOOL_CMD="appimage-run $TOOL_CMD"

$TOOL_CMD --no-appstream "$APP_DIR" "$APPIMAGE_OUTPUT"

rm -f "$APPIMAGETOOL_NAME"
echo "Build complete!"
