#!/bin/bash
# Wrapper for the pinned Unity editor CLI. All agent Unity work goes through
# this. Usage: scripts/unity.sh [extra unity args]
# Env overrides: UNITY_BIN, UNITY_PROJECT
set -euo pipefail

UNITY_BIN="${UNITY_BIN:-/Applications/Unity/Hub/Editor/6000.1.14f1/Unity.app/Contents/MacOS/Unity}"
REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY_PROJECT="${UNITY_PROJECT:-$REPO_ROOT/game}"

if [ ! -x "$UNITY_BIN" ]; then
    echo "unity.sh: editor not found at $UNITY_BIN" >&2
    exit 1
fi

exec "$UNITY_BIN" -batchmode -nographics \
    -projectPath "$UNITY_PROJECT" \
    -logFile - \
    "$@"
