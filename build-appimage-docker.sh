#!/bin/bash
set -e

echo "Building PoE Kompanion AppImage in Docker container..."

# Extract version from csproj
VERSION=$(grep -oP '<AppVersion>\K[^<]+' PoEKompanion.csproj)
APPIMAGE_NAME="PoE-Kompanion-${VERSION}-x86_64.AppImage"

# Clean old AppImage
rm -f "$APPIMAGE_NAME"

# Build the Docker image and export the AppImage
echo "Building AppImage in isolated container..."
docker build --network host -f Dockerfile.appimage --target export --output . .

echo ""
echo "Build complete! AppImage created: $APPIMAGE_NAME"
echo "To run: ./$APPIMAGE_NAME"
echo ""
