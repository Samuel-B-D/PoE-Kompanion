#!/bin/bash
set -e

echo "Building PoE Kompanion AppImage..."

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

# Create AppImage
echo "Creating AppImage..."
ARCH=x86_64 ./appimagetool-x86_64.AppImage AppDir PoE-Kompanion-x86_64.AppImage

echo "Build complete! AppImage created: PoE-Kompanion-x86_64.AppImage"
echo ""
echo "To run: ./PoE-Kompanion-x86_64.AppImage"
