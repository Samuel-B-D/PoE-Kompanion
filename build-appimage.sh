#!/bin/bash
set -e

echo "Building PoE Kompanion AppImage..."

# Extract version from csproj
VERSION=$(grep -oP '<AppVersion>\K[^<]+' PoEKompanion.csproj)
echo "Version: $VERSION"

# Clean previous build artifacts
echo "Cleaning previous build artifacts..."
rm -rf AppDir/usr/bin/*
rm -f PoE-Kompanion-*.AppImage

# Build the application
echo "Building application..."
dotnet publish -c Release -r linux-x64 --self-contained

# Copy the published application
echo "Copying application files..."
cp -r bin/Release/net9.0/linux-x64/publish/* AppDir/usr/bin/

# Download appimagetool if not present
if [ ! -f "appimagetool-x86_64.AppImage" ]; then
    echo "Downloading appimagetool..."
    wget -q "https://github.com/AppImage/AppImageKit/releases/download/continuous/appimagetool-x86_64.AppImage"
    chmod +x appimagetool-x86_64.AppImage
fi

# Create AppImage with embedded icon
echo "Creating AppImage..."
APPIMAGE_NAME="PoE-Kompanion-${VERSION}-x86_64.AppImage"
ARCH=x86_64 ./appimagetool-x86_64.AppImage --comp gzip AppDir "$APPIMAGE_NAME"

echo "Build complete! AppImage created: $APPIMAGE_NAME"
echo ""
echo "To run: ./$APPIMAGE_NAME"
