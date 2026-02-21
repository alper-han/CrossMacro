#!/usr/bin/env bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/lib/version.sh
source "$SCRIPT_DIR/lib/version.sh"

APP_NAME="CrossMacro"
VERSION="$(get_version)"
PUBLISH_DIR="${PUBLISH_DIR:-../publish}"
APP_DIR="AppDir"
APPIMAGETOOL_NAME="appimagetool-x86_64.AppImage"
APPIMAGETOOL_RELEASE_API="https://api.github.com/repos/AppImage/appimagetool/releases/tags/continuous"

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
    if ! release_json="$(curl -fsSL "$APPIMAGETOOL_RELEASE_API")"; then
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

mkdir -p "$APP_DIR/usr/bin" "$APP_DIR/usr/lib" "$APP_DIR/usr/share/icons/hicolor" \
         "$APP_DIR/usr/share/applications" "$APP_DIR/usr/share/metainfo"

cp -r "$PUBLISH_DIR/"* "$APP_DIR/usr/bin/"

LIBXTST_PATH=""
if [ -f "/usr/lib/x86_64-linux-gnu/libXtst.so.6" ]; then
    LIBXTST_PATH="/usr/lib/x86_64-linux-gnu/libXtst.so.6"
elif [ -f "/usr/lib64/libXtst.so.6" ]; then
    LIBXTST_PATH="/usr/lib64/libXtst.so.6"
elif [ -n "$LD_LIBRARY_PATH" ]; then
    IFS=':' read -ra ADDR <<< "$LD_LIBRARY_PATH"
    for dir in "${ADDR[@]}"; do
        if [ -f "$dir/libXtst.so.6" ]; then
            LIBXTST_PATH="$dir/libXtst.so.6"
            break
        fi
    done
fi

if [ -n "$LIBXTST_PATH" ]; then
    echo "Bundling libXtst.so.6 from: $LIBXTST_PATH"
    cp "$LIBXTST_PATH" "$APP_DIR/usr/lib/"
else
    echo "WARNING: libXtst.so.6 not found. XTest support may be missing."
fi

command -v patchelf >/dev/null && \
    patchelf --set-interpreter /lib64/ld-linux-x86-64.so.2 "$APP_DIR/usr/bin/CrossMacro.UI"

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
    curl -fL -o "$APPIMAGETOOL_NAME" \
        "https://github.com/AppImage/appimagetool/releases/download/continuous/$APPIMAGETOOL_NAME"
fi

verify_sha256 "$APPIMAGETOOL_NAME" "$APPIMAGETOOL_SHA256_RESOLVED"
chmod +x "$APPIMAGETOOL_NAME"

export ARCH=x86_64 PATH=$PWD:$PATH
TOOL_CMD="./$APPIMAGETOOL_NAME"
command -v appimage-run &>/dev/null && TOOL_CMD="appimage-run $TOOL_CMD"

$TOOL_CMD --no-appstream "$APP_DIR" "CrossMacro-$VERSION-x86_64.AppImage"

rm -f "$APPIMAGETOOL_NAME"
echo "Build complete!"
