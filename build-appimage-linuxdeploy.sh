#!/bin/bash
set -e

echo "Building PoE Kompanion AppImage with linuxdeploy..."

# Extract version from csproj
VERSION=$(grep -oP '<AppVersion>\K[^<]+' PoEKompanion.csproj)
echo "Version: $VERSION"

# Clean previous build artifacts
echo "Cleaning previous build artifacts..."
rm -rf AppDir
rm -f PoE-Kompanion-*.AppImage

# Build the application
echo "Building application..."
dotnet publish -c Release -r linux-x64 --self-contained

# Icon and desktop file are in project root (will be passed to linuxdeploy)

# Download and extract linuxdeploy if not present
if [ ! -d "linuxdeploy-extracted" ]; then
    echo "Downloading linuxdeploy..."
    if [ ! -f "linuxdeploy-x86_64.AppImage" ]; then
        wget -q "https://github.com/linuxdeploy/linuxdeploy/releases/download/continuous/linuxdeploy-x86_64.AppImage"
        chmod +x linuxdeploy-x86_64.AppImage
    fi

    echo "Extracting linuxdeploy (works without FUSE)..."
    ./linuxdeploy-x86_64.AppImage --appimage-extract >/dev/null 2>&1
    mv squashfs-root linuxdeploy-extracted
fi

# Use linuxdeploy to bundle dependencies and create AppImage
echo "Running linuxdeploy to bundle dependencies and create AppImage..."
APPIMAGE_BUILD_NAME="PoE_Kompanion-x86_64.AppImage"
APPIMAGE_NAME="PoE-Kompanion-${VERSION}-x86_64.AppImage"

# Build linuxdeploy arguments for all .so files
LINUXDEPLOY_ARGS=(
    --appdir AppDir
    --executable bin/Release/net9.0/linux-x64/publish/PoEKompanion
    --desktop-file poe-kompanion.desktop
    --icon-file poe-kompanion.png
)

# Add all .so files as libraries
for lib in bin/Release/net9.0/linux-x64/publish/*.so; do
    if [ -f "$lib" ]; then
        LINUXDEPLOY_ARGS+=(--library "$lib")
    fi
done

LINUXDEPLOY_ARGS+=(--output appimage)

OUTPUT_NAME="$APPIMAGE_BUILD_NAME" \
ARCH=x86_64 \
./linuxdeploy-extracted/AppRun "${LINUXDEPLOY_ARGS[@]}"

# Rename to our preferred naming
if [ -f "$APPIMAGE_BUILD_NAME" ]; then
    echo "moving \"$APPIMAGE_BUILD_NAME\" to \"$APPIMAGE_NAME\""
    mv "$APPIMAGE_BUILD_NAME" "$APPIMAGE_NAME"
fi

echo "Cleaning up AppDir post-build..."
rm -rf AppDir

echo ""
echo "Build complete! AppImage created: $APPIMAGE_NAME"
echo ""
echo "To run: ./$APPIMAGE_NAME"
echo ""
