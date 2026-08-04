#!/bin/bash
# PreToolUse hook: blocks source file edits when symbolic alignment is broken or stale.
# Wraps `align.py check --quiet` and converts exit codes into deny decisions.
#
# Exit codes from align.py:
#   0 = aligned (allow)
#   1 = broken interlocks or missing docs (deny)
#   2 = stale lock — manifest changed but lock not regenerated (deny)

set -uo pipefail

INPUT=$(cat)
FILE_PATH=$(echo "$INPUT" | jq -r '.tool_input.file_path // empty')

[ -z "$FILE_PATH" ] && exit 0

# Only gate production files
case "$FILE_PATH" in
    */game/Assets/*|*/K8s/*|*/scripts/*|*/server/*) ;;
    *) exit 0 ;;
esac

PROJ="${CLAUDE_PROJECT_DIR:-$(echo "$INPUT" | jq -r '.cwd')}"

# Run alignment check
alignment_output=$(python3 "$PROJ/scripts/align.py" check --quiet 2>&1)
exit_code=$?

case $exit_code in
    0)
        # Aligned — allow
        exit 0
        ;;
    1)
        # Broken interlocks or missing docs
        jq -n --arg reason "ALIGNMENT BROKEN: ${alignment_output}. Stop and fix the root cause. Run 'python3 scripts/align.py status' for details, then 'python3 scripts/align.py lock' after fixing." \
        '{
            hookSpecificOutput: {
                hookEventName: "PreToolUse",
                permissionDecision: "deny",
                permissionDecisionReason: $reason
            }
        }'
        exit 0
        ;;
    2)
        # Stale lock
        jq -n --arg reason "ALIGNMENT STALE: ${alignment_output}. Run 'python3 scripts/align.py lock' to regenerate the lock before editing source files." \
        '{
            hookSpecificOutput: {
                hookEventName: "PreToolUse",
                permissionDecision: "deny",
                permissionDecisionReason: $reason
            }
        }'
        exit 0
        ;;
    *)
        # align.py itself errored (missing file, syntax error, etc.) — don't block
        exit 0
        ;;
esac
