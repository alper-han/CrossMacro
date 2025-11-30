#!/bin/bash
set -e

# Configuration
APP_NAME="CrossMacro"
VERSION="${VERSION:-1.0.0}"
PUBLISH_DIR="${PUBLISH_DIR:-../publish}"  # Use env var or default to ../publish
APP_DIR="AppDir"
ICON_PATH="../src/CrossMacro.UI/Assets/mouse-icon.png"

# Clean previous build
rm -rf "$APP_DIR"

# Verify publish directory exists
if [ ! -d "$PUBLISH_DIR" ]; then
    echo "Error: Publish directory not found: $PUBLISH_DIR"
    echo "Please build the application first or set PUBLISH_DIR environment variable"
    exit 1
fi

echo "Using pre-built binaries from: $PUBLISH_DIR"

# 1. Create AppDir structure
echo "Creating AppDir structure..."
mkdir -p "$APP_DIR/usr/bin"
mkdir -p "$APP_DIR/usr/share/icons/hicolor/256x256/apps"
mkdir -p "$APP_DIR/usr/share/applications"
mkdir -p "$APP_DIR/usr/share/metainfo"

# 2. Copy files
echo "Copying files..."
cp -r "$PUBLISH_DIR/"* "$APP_DIR/usr/bin/"
cp "$ICON_PATH" "$APP_DIR/crossmacro.png"
cp "$ICON_PATH" "$APP_DIR/usr/share/icons/hicolor/256x256/apps/crossmacro.png"
cp "assets/$APP_NAME.desktop" "$APP_DIR/$APP_NAME.desktop"
cp "assets/$APP_NAME.desktop" "$APP_DIR/usr/share/applications/$APP_NAME.desktop"
cp "assets/com.github.alper-han.CrossMacro.appdata.xml" "$APP_DIR/usr/share/metainfo/com.github.alper-han.CrossMacro.appdata.xml"

# 3. Create AppRun symlink
echo "Creating AppRun..."
# Ensure the binary is executable
chmod +x "$APP_DIR/usr/bin/CrossMacro.UI"
ln -s "usr/bin/CrossMacro.UI" "$APP_DIR/AppRun"

# 4. Download appimagetool if not exists
if [ ! -f "appimagetool-x86_64.AppImage" ]; then
    echo "Downloading appimagetool..."
    curl -L -o appimagetool-x86_64.AppImage "https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage"
    chmod +x appimagetool-x86_64.AppImage
fi

# 5. Generate AppImage
echo "Generating AppImage..."
export ARCH=x86_64
export PATH=$PWD:$PATH
./appimagetool-x86_64.AppImage "$APP_DIR" "CrossMacro-$VERSION-x86_64.AppImage"

# 7. Cleanup appimagetool
echo "Cleaning up build tools..."
rm -f appimagetool-x86_64.AppImage

echo "Build complete!"
