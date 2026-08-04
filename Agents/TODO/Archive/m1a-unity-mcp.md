# M1a — Unity MCP bridge install (time-boxed trial)

## Author: fable-primary
## Status: Complete (2026-08-04) — bridge verified: 8090 up in ~10s, MCP handshake OK, guardrail blocks, tests green after teardown

Install CoderGamester/mcp-unity so agents can drive a persistent editor
process (live console, no batchmode cold starts). Guardrails from the M0b
review stand: batchmode `run-tests.sh` stays the merge gate; the MCP editor
must release the project lock before batchmode runs.

## Plan

1. `game/Packages/manifest.json`: add
   `"com.gamelovers.mcp-unity": "https://github.com/CoderGamester/mcp-unity.git"`.
   Resolve via a `-quit` batchmode run. (Prereqs verified: Node 22.16,
   npm 10.9.)
2. Build the Node server: `npm install && npm run build` in
   `game/Library/PackageCache/com.gamelovers.mcp-unity*/Server~`.
3. `game/ProjectSettings/McpUnitySettings.json`: enable auto-start of the
   WebSocket bridge (port 8090) so no menu click is ever needed. If the
   package has no auto-start setting, add an `[InitializeOnLoad]` editor
   script in Potshot.EditorTools that starts it when a non-batchmode editor
   loads.
4. Repo-root `.mcp.json` (project-scope MCP config, committed): entry
   `mcp-unity` → `bash scripts/mcp-unity-server.sh`; the wrapper globs the
   PackageCache path (hash changes on package updates) and execs
   `node .../Server~/build/index.js`.
5. `scripts/editor-daemon.sh`: start/stop/status for the agent-owned editor
   (`Unity -projectPath game`, windowed, nohup; stop = SIGTERM so the
   project lock releases cleanly). Document in docs/agents.md: ALWAYS
   `editor-daemon.sh stop` before `run-tests.sh`/`build-server.sh`.
6. Verify: daemon starts → port 8090 listening (`nc -z`); node server
   handshakes; batchmode tests still green after daemon stop.
7. Note: the new MCP server appears to Claude Code sessions after restart /
   .mcp.json approval — record in docs/agents.md.

## Trial exit criteria (evaluate at end of M1)

Keep if it measurably speeds the code→compile→inspect loop; otherwise remove
package + .mcp.json and record why in this task doc.
