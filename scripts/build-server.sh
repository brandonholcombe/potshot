#!/bin/bash
# Build the headless Linux dedicated server into server/build/, then
# (optionally) the linux/amd64 Docker image (tow-c1 nodes are amd64 —
# a default docker build on this arm64 Mac would CrashLoop there).
# Usage: scripts/build-server.sh [--docker]
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$REPO_ROOT/server/build"
VERSION="$(cd "$REPO_ROOT" && git rev-parse --short HEAD 2>/dev/null || echo dev)"

# Guardrail: one process owns the Unity project lock.
if [ -f "$REPO_ROOT/.editor-daemon.pid" ] && kill -0 "$(cat "$REPO_ROOT/.editor-daemon.pid")" 2>/dev/null; then
    echo "build-server.sh: editor daemon is running — 'scripts/editor-daemon.sh stop' first" >&2
    exit 1
fi

rm -rf "$OUT"
mkdir -p "$OUT"
"$REPO_ROOT/scripts/unity.sh" -quit \
    -executeMethod Potshot.EditorTools.Builder.BuildLinuxServer \
    -potshotOut "$OUT" -potshotVersion "$VERSION"

echo "Server build at $OUT (git $VERSION)"

if [ "${1:-}" = "--docker" ]; then
    docker buildx build --platform linux/amd64 --load \
        -t "bholcombe/potshot-server:$VERSION" \
        -t "bholcombe/potshot-server:dev" \
        -f "$REPO_ROOT/server/Dockerfile" "$REPO_ROOT/server"
    echo "Image: bholcombe/potshot-server:$VERSION (linux/amd64)"
fi
