#!/bin/bash
set -e

echo "Publishing PoE Kompanion..."

# Extract version from csproj
VERSION=$(grep -oP '<AppVersion>\K[^<]+' PoEKompanion.csproj)
echo "Version: $VERSION"

# Create publish directory
PUBLISH_DIR="publish/${VERSION}"
mkdir -p "$PUBLISH_DIR"

# Build using Docker and export artifacts
echo "Building in Docker container..."
docker build --network host -f Dockerfile.appimage --target export --output "$PUBLISH_DIR" .

echo ""
echo "Build complete!"
echo "Artifacts published to: $PUBLISH_DIR/"
echo "  - PoE-Kompanion-${VERSION}-x86_64.AppImage"
echo "  - PoE-Kompanion-${VERSION}-x86_64.tar.gz"
echo ""
