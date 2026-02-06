#!/usr/bin/env bash
set -e

APP_NAME="CrossMacro"
VERSION="${VERSION="0.9.5"}"
PUBLISH_DIR="${PUBLISH_DIR:-../publish}"
APP_DIR="AppDir"

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

if [ ! -f "appimagetool-x86_64.AppImage" ]; then
    curl -L -o appimagetool-x86_64.AppImage \
        "https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage"
    chmod +x appimagetool-x86_64.AppImage
fi

export ARCH=x86_64 PATH=$PWD:$PATH
TOOL_CMD="./appimagetool-x86_64.AppImage"
command -v appimage-run &>/dev/null && TOOL_CMD="appimage-run $TOOL_CMD"

$TOOL_CMD --no-appstream "$APP_DIR" "CrossMacro-$VERSION-x86_64.AppImage"

rm -f appimagetool-x86_64.AppImage
echo "Build complete!"
