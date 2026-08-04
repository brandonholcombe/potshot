#!/bin/bash
# Run EditMode and PlayMode tests in batchmode. Exit nonzero on any failure.
# Usage: scripts/run-tests.sh [EditMode|PlayMode]   (default: both)
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
RESULTS_DIR="$REPO_ROOT/game/Logs/test-results"
mkdir -p "$RESULTS_DIR"

# Guardrail (M0b/M1a reviews): only one process may hold the project lock.
if [ -f "$REPO_ROOT/.editor-daemon.pid" ] && kill -0 "$(cat "$REPO_ROOT/.editor-daemon.pid")" 2>/dev/null; then
    echo "run-tests.sh: editor daemon is running — 'scripts/editor-daemon.sh stop' first" >&2
    exit 1
fi

run_platform() {
    local platform="$1"
    echo "=== $platform tests ==="
    "$REPO_ROOT/scripts/unity.sh" \
        -runTests -testPlatform "$platform" \
        -testResults "$RESULTS_DIR/$platform.xml"
    local code=$?
    echo "$platform exit: $code (results: game/Logs/test-results/$platform.xml)"
    return $code
}

overall=0
for p in ${1:-EditMode PlayMode}; do
    run_platform "$p" || overall=1
done
exit $overall
