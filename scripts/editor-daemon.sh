#!/bin/bash
# Agent-owned persistent Unity editor (for the MCP bridge). Never requires a
# human click — but a WINDOWED editor needs the logged-in macOS GUI session;
# over SSH stay batchmode-only (do not use this script).
#
# Usage: scripts/editor-daemon.sh start|stop|status
# Rule (M0b review): the daemon MUST be stopped before run-tests.sh or
# build-server.sh — one process owns the project lock at a time.
# run-tests.sh enforces this and fails fast if the daemon is up.
set -uo pipefail

UNITY_BIN="${UNITY_BIN:-/Applications/Unity/Hub/Editor/6000.1.14f1/Unity.app/Contents/MacOS/Unity}"
REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT="$REPO_ROOT/game"
PIDFILE="$REPO_ROOT/.editor-daemon.pid"
LOCKFILE="$PROJECT/Temp/UnityLockfile"
LOG="$PROJECT/Logs/editor-daemon.log"

alive() { [ -f "$PIDFILE" ] && kill -0 "$(cat "$PIDFILE")" 2>/dev/null; }

case "${1:-status}" in
    start)
        if alive; then echo "already running (pid $(cat "$PIDFILE"))"; exit 0; fi
        mkdir -p "$PROJECT/Logs"
        nohup "$UNITY_BIN" -projectPath "$PROJECT" -logFile "$LOG" \
            >/dev/null 2>&1 &
        echo $! > "$PIDFILE"
        echo "started (pid $(cat "$PIDFILE"), log $LOG)"
        ;;
    stop)
        if ! alive; then echo "not running"; rm -f "$PIDFILE"; exit 0; fi
        kill "$(cat "$PIDFILE")"
        for _ in $(seq 1 30); do
            alive || break
            sleep 1
        done
        if alive; then echo "did not exit after 30s; kill -9-ing" >&2; kill -9 "$(cat "$PIDFILE")"; sleep 2; fi
        rm -f "$PIDFILE"
        # SIGTERM skips Unity's clean-quit path; with the process confirmed
        # dead the leftover lockfile is stale — remove it.
        rm -f "$LOCKFILE"
        echo "stopped, project lock released"
        ;;
    status)
        if alive; then echo "running (pid $(cat "$PIDFILE"))"; else echo "not running"; fi
        [ -f "$LOCKFILE" ] && echo "project lock: HELD" || echo "project lock: free"
        ;;
    *)
        echo "usage: $0 start|stop|status" >&2; exit 2
        ;;
esac
