#!/bin/bash
set -e

# Configuration
APP_NAME="crossmacro"
VERSION="${VERSION:-1.0.0}"
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

echo "Using pre-built binaries from: $PUBLISH_DIR"

# 1. Prepare RPM Build Directory
echo "Preparing RPM build directory..."
mkdir -p "$RPM_BUILD_DIR"/{BUILD,RPMS,SOURCES,SPECS,SRPMS}

# 2. Copy Assets to SOURCES
echo "Copying assets..."
cp -r "$PUBLISH_DIR" "$RPM_BUILD_DIR/SOURCES/publish"
cp "$ICON_PATH" "$RPM_BUILD_DIR/SOURCES/crossmacro.png"
cp "assets/CrossMacro.desktop" "$RPM_BUILD_DIR/SOURCES/CrossMacro.desktop"

# 3. Copy Spec File
cp "packaging/rpm/crossmacro.spec" "$RPM_BUILD_DIR/SPECS/"

# 4. Build RPM
echo "Building RPM package..."
if command -v rpmbuild &> /dev/null; then
    rpmbuild --define "_topdir $(pwd)/$RPM_BUILD_DIR" \
             --define "_sourcedir $(pwd)/$RPM_BUILD_DIR/SOURCES" \
             --define "version $VERSION" \
             -bb "$RPM_BUILD_DIR/SPECS/crossmacro.spec"
    
    # Copy RPM to scripts directory for GitHub release
    cp "$RPM_BUILD_DIR"/RPMS/x86_64/*.rpm .
    echo "RPM package created: $(ls *.rpm)"
else
    echo "Error: rpmbuild not found. Cannot build .rpm package."
    echo "The directory structure is ready in '$RPM_BUILD_DIR'."
    exit 1
fi
