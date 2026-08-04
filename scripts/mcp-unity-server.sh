#!/bin/bash
# Launches the mcp-unity Node server for Claude Code (via .mcp.json).
# Self-healing: Library/PackageCache is a disposable, machine-local cache —
# Unity can wipe it on any re-resolve — so this script re-locates the package
# and rebuilds the server whenever build/index.js is missing.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CACHE="$REPO_ROOT/game/Library/PackageCache"

pkg_dir() {
    ls -d "$CACHE"/com.gamelovers.mcp-unity@* 2>/dev/null | head -1
}

PKG="$(pkg_dir)"
if [ -z "$PKG" ]; then
    echo "mcp-unity: package not in PackageCache — resolving via Unity" >&2
    "$REPO_ROOT/scripts/unity.sh" -quit >&2
    PKG="$(pkg_dir)"
    [ -z "$PKG" ] && { echo "mcp-unity: resolve failed" >&2; exit 1; }
fi

SERVER="$PKG/Server~"
if [ ! -f "$SERVER/build/index.js" ]; then
    echo "mcp-unity: building Node server in $SERVER" >&2
    (cd "$SERVER" && npm install --silent && npm run build) >&2
fi

exec node "$SERVER/build/index.js"
