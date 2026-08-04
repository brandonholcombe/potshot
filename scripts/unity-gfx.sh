#!/bin/bash
# Graphics-capable Unity CLI wrapper: windowed editor (NO -batchmode /
# -nographics) for render probes and Visual tests. The editor window appears
# briefly on the Mac — no human interaction is ever required.
# Requires the logged-in macOS GUI session; refuses SSH sessions.
# Usage: scripts/unity-gfx.sh [unity args]
set -euo pipefail

UNITY_BIN="${UNITY_BIN:-/Applications/Unity/Hub/Editor/6000.1.14f1/Unity.app/Contents/MacOS/Unity}"
REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY_PROJECT="${UNITY_PROJECT:-$REPO_ROOT/game}"

if [ -n "${SSH_CONNECTION:-}" ]; then
    echo "unity-gfx.sh: SSH session — no GUI available, use batchmode instead" >&2
    exit 1
fi
if [ "$(launchctl managername 2>/dev/null)" != "Aqua" ]; then
    echo "unity-gfx.sh: no Aqua (GUI) session — cannot render" >&2
    exit 1
fi
# Single project lock (same rule as run-tests.sh)
if [ -f "$REPO_ROOT/.editor-daemon.pid" ] && kill -0 "$(cat "$REPO_ROOT/.editor-daemon.pid")" 2>/dev/null; then
    echo "unity-gfx.sh: editor daemon is running — 'scripts/editor-daemon.sh stop' first" >&2
    exit 1
fi

exec "$UNITY_BIN" -projectPath "$UNITY_PROJECT" -logFile - "$@"
