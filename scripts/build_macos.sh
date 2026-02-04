#!/usr/bin/env bash
set -e

APP_NAME="CrossMacro"
VERSION="${VERSION:-0.9.3}"
BUNDLE_ID="net.crossmacro.CrossMacro"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
OUTPUT_DIR="${OUTPUT_DIR:-$SCRIPT_DIR/macos_output}"
APP_BUNDLE="$OUTPUT_DIR/$APP_NAME.app"

echo "=== CrossMacro macOS Build Script ==="
echo "Version: $VERSION"
echo "Output: $OUTPUT_DIR"

rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"

RID="${RID:-osx-arm64}"

echo ""
echo "=== Publish .NET Application ==="
echo "Building for Runtime: $RID"

PUBLISH_DIR="$OUTPUT_DIR/publish"

dotnet publish "$PROJECT_ROOT/src/CrossMacro.UI/CrossMacro.UI.csproj" \
    -c Release \
    -r "$RID" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishReadyToRun=false \
    -p:Version="$VERSION" \
    -o "$PUBLISH_DIR"

echo "Published to: $PUBLISH_DIR"

echo ""
echo "=== Create .app Bundle ==="

mkdir -p "$APP_BUNDLE/Contents/MacOS"
mkdir -p "$APP_BUNDLE/Contents/Resources"

echo "Creating Info.plist..."
cat > "$APP_BUNDLE/Contents/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>$APP_NAME</string>
    <key>CFBundleDisplayName</key>
    <string>$APP_NAME</string>
    <key>CFBundleIdentifier</key>
    <string>$BUNDLE_ID</string>
    <key>CFBundleVersion</key>
    <string>$VERSION</string>
    <key>CFBundleShortVersionString</key>
    <string>$VERSION</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleSignature</key>
    <string>????</string>
    <key>CFBundleExecutable</key>
    <string>CrossMacro.UI</string>
    <key>CFBundleIconFile</key>
    <string>AppIcon</string>
    <key>LSMinimumSystemVersion</key>
    <string>10.15</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>NSHumanReadableCopyright</key>
    <string>Copyright © 2024 CrossMacro. All rights reserved.</string>
    <key>LSApplicationCategoryType</key>
    <string>public.app-category.utilities</string>
    <key>NSAppleEventsUsageDescription</key>
    <string>CrossMacro needs to send events to other applications for macro playback.</string>
</dict>
</plist>
EOF

echo "Copying application files..."
cp -R "$PUBLISH_DIR/"* "$APP_BUNDLE/Contents/MacOS/"

chmod +x "$APP_BUNDLE/Contents/MacOS/CrossMacro.UI"

ICON_PNG="$PROJECT_ROOT/src/CrossMacro.UI/Assets/mouse-icon.png"
ICON_ICNS="$APP_BUNDLE/Contents/Resources/AppIcon.icns"

if [[ "$OSTYPE" == "darwin"* ]]; then
    if [ -f "$ICON_PNG" ]; then
        echo "Creating AppIcon.icns from $ICON_PNG..."
        
        ICONSET_DIR="$OUTPUT_DIR/AppIcon.iconset"
        mkdir -p "$ICONSET_DIR"
        
        sips -z 16 16     "$ICON_PNG" --out "$ICONSET_DIR/icon_16x16.png" > /dev/null
        sips -z 32 32     "$ICON_PNG" --out "$ICONSET_DIR/icon_16x16@2x.png" > /dev/null
        sips -z 32 32     "$ICON_PNG" --out "$ICONSET_DIR/icon_32x32.png" > /dev/null
        sips -z 64 64     "$ICON_PNG" --out "$ICONSET_DIR/icon_32x32@2x.png" > /dev/null
        sips -z 128 128   "$ICON_PNG" --out "$ICONSET_DIR/icon_128x128.png" > /dev/null
        sips -z 256 256   "$ICON_PNG" --out "$ICONSET_DIR/icon_128x128@2x.png" > /dev/null
        sips -z 256 256   "$ICON_PNG" --out "$ICONSET_DIR/icon_256x256.png" > /dev/null
        sips -z 512 512   "$ICON_PNG" --out "$ICONSET_DIR/icon_256x256@2x.png" > /dev/null
        sips -z 512 512   "$ICON_PNG" --out "$ICONSET_DIR/icon_512x512.png" > /dev/null
        sips -z 1024 1024 "$ICON_PNG" --out "$ICONSET_DIR/icon_512x512@2x.png" > /dev/null
        
        iconutil -c icns "$ICONSET_DIR" -o "$ICON_ICNS"
        rm -rf "$ICONSET_DIR"
    else
        echo "Warning: Icon file not found at $ICON_PNG"
    fi
else
    if [ -f "$ICON_PNG" ]; then
         echo "Not on macOS, skipping icns generation. Copying png as placeholder."
         cp "$ICON_PNG" "$APP_BUNDLE/Contents/Resources/AppIcon.png"
    fi
fi

echo "APPL????" > "$APP_BUNDLE/Contents/PkgInfo"

rm -rf "$PUBLISH_DIR"

echo ""
echo "=== Packaging ==="

if [[ "$OSTYPE" == "darwin"* ]]; then
    DMG_NAME="$APP_NAME-$VERSION-$RID.dmg"
    DMG_PATH="$OUTPUT_DIR/$DMG_NAME"
    
    echo "Creating DMG: $DMG_PATH"
    
    if command -v create-dmg &> /dev/null; then
        echo "Using create-dmg utility..."
        
        if [ -f "$DMG_PATH" ]; then
            rm "$DMG_PATH"
        fi

        CREATE_DMG_ARGS=(--volname "$APP_NAME Installer")
        
        if [ -f "$ICON_ICNS" ]; then
            CREATE_DMG_ARGS+=(--volicon "$ICON_ICNS")
        fi
        
        CREATE_DMG_ARGS+=(
          --window-pos 200 120
          --window-size 600 400
          --icon-size 100
          --icon "$APP_NAME.app" 150 190
          --hide-extension "$APP_NAME.app"
          --app-drop-link 450 185
          "$DMG_PATH"
          "$APP_BUNDLE"
        )
        
        if create-dmg "${CREATE_DMG_ARGS[@]}"; then
            echo "DMG created successfully with create-dmg"
        else
            if [ -f "$DMG_PATH" ]; then
                echo "create-dmg returned non-zero but DMG was created (likely a warning)"
            else
                echo "create-dmg failed, falling back to hdiutil..."
                hdiutil create -volname "$APP_NAME" -srcfolder "$APP_BUNDLE" -ov -format UDZO "$DMG_PATH"
            fi
        fi
    else
        echo "create-dmg not found, using hdiutil..."
        hdiutil create -volname "$APP_NAME" -srcfolder "$APP_BUNDLE" -ov -format UDZO "$DMG_PATH"
    fi
    
    echo "DMG Created at: $DMG_PATH"
else
    echo "Not running on macOS, skipping DMG creation."
    
    ARCHIVE_NAME="$APP_NAME-$VERSION-$RID-macos.zip"
    ARCHIVE_PATH="$OUTPUT_DIR/$ARCHIVE_NAME"
    
    echo "Creating ZIP archive: $ARCHIVE_PATH"
    cd "$OUTPUT_DIR"
    if command -v zip &> /dev/null; then
        zip -r "$ARCHIVE_NAME" "$APP_NAME.app"
    else
        echo "Warning: zip command not found."
    fi
    cd - > /dev/null
fi

echo ""
echo "=== Build Complete ==="
