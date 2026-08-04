#!/bin/bash
# Build the headless Linux dedicated server into server/build/, then
# (optionally) the Docker image. Requires the linux-server Unity module.
# Usage: scripts/build-server.sh [--docker]
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$REPO_ROOT/server/build"
VERSION="$(cd "$REPO_ROOT" && git rev-parse --short HEAD 2>/dev/null || echo dev)"

mkdir -p "$OUT"
"$REPO_ROOT/scripts/unity.sh" -quit \
    -executeMethod Potshot.EditorTools.Builder.BuildLinuxServer \
    -potshotOut "$OUT" -potshotVersion "$VERSION"

echo "Server build at $OUT (version $VERSION)"

if [ "${1:-}" = "--docker" ]; then
    docker build -t "bholcombe/potshot-server:$VERSION" \
        -t "bholcombe/potshot-server:dev" \
        -f "$REPO_ROOT/server/Dockerfile" "$REPO_ROOT/server"
    echo "Image: bholcombe/potshot-server:$VERSION"
fi
