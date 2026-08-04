# Review — m1a-unity-mcp.md

## Reviewer: sonnet-reviewer
Task: `Agents/TODO/Active/m1a-unity-mcp.md` (Author: fable-primary)

## Verdict: approve-with-changes

Upstream verified against CoderGamester/mcp-unity (package.json + Editor source).
The plan's shape is right; step 3's fallback is unnecessary, and steps 1–2 and 5
need hardening before implementation.

## Findings

1. **Step 3 fallback: delete it — the package already does this.**
   `McpUnitySettings.cs` has `AutoStartServer` (bool, **default `true`**) in
   `ProjectSettings/McpUnitySettings.json`, and `McpUnityServer` is
   `[InitializeOnLoad]` with `Application.isBatchMode` guards in three places
   (Instance getter, constructor, scheduled start). So: commit
   `McpUnitySettings.json` with `"AutoStartServer": true` and drop the custom
   `[InitializeOnLoad]` script entirely — a second auto-starter would race the
   package's own. Batchmode pollution (my checklist concern) is already handled
   upstream; do not re-implement it. Ensure `NpmExecutablePath` stays empty in
   the committed file (it is machine-local).

2. **Step 1: package key verified, but pin the ref.** `"com.gamelovers.mcp-unity"`
   matches upstream `package.json` (v1.4.0, Unity ≥2022.3; Node ≥18 — 22.16 ok).
   A bare `.git` URL re-resolves to whatever HEAD is: irreproducible across
   machines and it churns the PackageCache hash, breaking the wrapper's cached
   build (finding 3). Pin: `"...mcp-unity.git#v1.4.0"` (or a commit SHA), and
   commit `Packages/packages-lock.json` in the same change (M0b finding 1 pattern).

3. **Step 2: PackageCache is the wrong home for the npm build.** Unity treats
   `Library/PackageCache` as an immutable, disposable cache — any re-resolve,
   `Library/` wipe, or package bump deletes `node_modules` + `build/` silently;
   in Unity 6 cache entries may also be shared with the global package cache, so
   writing into them pollutes state beyond this project. Since `Library/` is
   gitignored, the committed `.mcp.json` + wrapper only work after a per-machine
   build anyway. Required change: make `scripts/mcp-unity-server.sh` self-healing —
   glob the path, and if `Server~/build/index.js` is missing, run
   `npm install && npm run build` before exec. If the glob matches multiple
   `com.gamelovers.mcp-unity@*` dirs (stale hashes), pick the one named in
   `packages-lock.json`, not "first match". A cleaner alternative worth one line
   of consideration: copy `Server~` to a stable repo path (e.g. `tools/mcp-unity/`,
   gitignore `node_modules`/`build`) — but the self-healing wrapper is acceptable.

4. **Step 5: daemon script gaps.** Direct-binary launch
   (`.../Unity.app/Contents/MacOS/Unity -projectPath <abs>`) is correct on macOS —
   no `open -a` needed (and `open` detaches the PID you need). But: (a) windowed
   Unity needs the logged-in Aqua session; from a pure SSH session window/Metal
   creation can fail — document "run from the console session or a tmux started
   there" as a known limit, batchmode remains the SSH path. (b) Use an absolute
   `-projectPath` and a pidfile; never `pgrep Unity` (would kill concurrent
   batchmode runs). (c) Pass `-logFile <file>` so agents can read editor output.
   (d) `stop` must wait for process exit **and** verify `game/Temp/UnityLockfile`
   is gone before returning; SIGKILL-fallback leaves the lock behind.

5. **Lock guardrail: enforce, don't just document.** "ALWAYS stop before
   run-tests.sh" in docs will eventually be skipped by some agent. Add a cheap
   check to `run-tests.sh`/`build-server.sh` (or `unity.sh`): if the daemon
   pidfile is live (or `Temp/UnityLockfile` exists with a live owner), fail fast
   with "stop editor-daemon first" instead of letting Unity hang on the lock.

6. **Security: acceptable, with two hard rules.** The bridge binds
   `localhost:8090` only when `AllowRemoteConnections=false` (verified in source:
   `host = AllowRemoteConnections ? "0.0.0.0" : "localhost"`). The socket is
   unauthenticated and lets a connecting process drive the editor (execute menu
   items, run code) — on a single-user dev Mac that is an accepted local risk,
   but (a) `AllowRemoteConnections` must never be set true and the committed
   settings file must say `false`, and (b) this is a dev-machine-only tool: it
   never appears in `server/`, `K8s/`, or any deployed image, so no
   hostNetwork/K8s exposure question arises. State both in docs/agents.md.

7. **Minor.** Step 4's `.mcp.json` requires per-user approval plus a Claude Code
   restart — plan already notes this; also note `UNITY_PORT` env var exists if
   8090 ever collides. Trial exit criteria are good; removal must also delete
   `McpUnitySettings.json` and the wrapper script, not just package + `.mcp.json`.
