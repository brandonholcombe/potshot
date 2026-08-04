# Agent Playbook — building Unity without the editor GUI

The Prime Directive (CLAUDE.md): no task may require a human in the Unity
editor. This file is the how.

## Unity CLI basics

`scripts/unity.sh` wraps the pinned editor
(`/Applications/Unity/Hub/Editor/6000.1.14f1/.../Unity`) and always passes
`-batchmode -nographics -logFile -` plus the project path. Typical calls:

```bash
scripts/unity.sh -quit -executeMethod Potshot.EditorTools.SceneBuilder.BuildAll
scripts/unity.sh -quit -importPackage /path/to/FishNet.unitypackage
scripts/run-tests.sh              # EditMode + PlayMode via -runTests
scripts/build-server.sh           # BuildPipeline call → Linux headless player
```

Notes:
- Batchmode fails without an activated license — if you see
  "No valid Unity Editor license found", stop and file a Human task.
- Only one Unity instance may hold the project lock at a time. Serialize
  Unity invocations; never run two batchmode calls concurrently.
- First run after adding packages does a long import — timeouts of 10 min
  are normal; pass `timeout: 600000` to Bash.

## Scenes & prefabs: editor scripts, not YAML edits

Scenes/prefabs are committed YAML, but agents **never hand-edit** them —
generation is reproducible, hand edits aren't. All construction goes through
editor scripts under `game/Assets/Editor/` (e.g. `SceneBuilder`,
`PrefabFactory`, `ProjectConfigurator` for tags/layers/physics/quality
settings). Re-running a builder must be idempotent.

## Gameplay code layout

- `game/Assets/Scripts/Runtime/` — runtime assemblies (`Potshot.Core`,
  `Potshot.Net`, later `Potshot.Steam`), each with an `.asmdef`.
- `game/Assets/Scripts/Editor/` — editor tooling assembly.
- `game/Assets/Tests/` — `EditMode/` and `PlayMode/` test assemblies.
- Tank/weapon definitions are ScriptableObjects **created by editor scripts**
  from plain C# data tables — the data lives in code, reviewable in diffs.

## Seeing without eyes: the QA harness

1. **Screenshot rig** (M1b, working): PNGs land in `game/Logs/qa/` for
   agents to Read and judge. Two paths, both via `unity-gfx.sh` (windowed —
   never over SSH; editor window appears briefly, no clicks):
   ```bash
   # Static scene composition (edit mode, fast):
   scripts/unity-gfx.sh -executeMethod Potshot.EditorTools.QaScreenshots.RenderProbe \
       [-potshotScene Assets/Scenes/Foo.unity]   # exits itself; do NOT pass -quit
   # During simulation (Visual tests — editor-only asmdef, EnterPlayMode + QaCapture):
   scripts/unity-gfx.sh -runTests -testPlatform EditMode \
       -assemblyNames Potshot.Tests.Visual -testResults "$PWD/game/Logs/test-results/Visual.xml"
   ```
   Gotchas encoded in the code: editor-only test assemblies are EDITMODE
   assemblies to Unity (hence EnterPlayMode, not -testPlatform PlayMode);
   capture is synchronous camera→RenderTexture (no WaitForEndOfFrame);
   Built-in RP assumed (loud failure if the project moves to URP/HDRP).
   The batchmode merge gate excludes Visual tests via `-assemblyNames`.
2. **Headless netcode runner** (M2): launches the server build + N client
   processes with synthetic input scripts, asserts on end-state (positions,
   kills, pickups) written to JSON, then exits nonzero on violation.
3. **Feel numbers**: `docs/gameplay.md` feel targets are asserted in tests
   (top speed, time-to-kill) so blind tuning stays inside sane bounds.

## Unity MCP bridge (iteration mode)

`com.gamelovers.mcp-unity` (pinned `#v1.4.0`) gives agents a persistent
editor: live console, compile status, scene queries — no batchmode cold
start per iteration. Registered project-scope in `.mcp.json` (new Claude
Code sessions see it after the user approves the project MCP config; the
session that installed it must restart to see the tools).

- `scripts/editor-daemon.sh start|stop|status` — the agent launches/owns the
  editor process. Windowed editor requires the logged-in macOS GUI session;
  over SSH, stay batchmode-only.
- The package auto-starts its WebSocket bridge on **localhost:8090** when a
  non-batchmode editor loads (`ProjectSettings/McpUnitySettings.json`,
  `AutoStartServer`; keep `AllowRemoteConnections: false` — the socket is
  unauthenticated). It correctly does nothing in batchmode runs.
- **Lock rule**: stop the daemon before `run-tests.sh` / `build-server.sh`
  (enforced — run-tests fails fast if the daemon pidfile is alive).
  Batchmode tests remain the merge gate; MCP is iteration speed only.
- `Library/PackageCache` is disposable: `scripts/mcp-unity-server.sh`
  re-resolves and rebuilds the Node server automatically if Unity wiped it.

## Human interface

- Blockers only → task file in `Agents/TODO/Human/` with: exact steps, why
  agents can't do it, expected minutes, and a pre-filled `HUMAN_INPUT.log`
  line for Brandon to append on completion.
- Playtest feedback arrives as free-form notes in `Agents/TODO/Backlog/` —
  triage into task docs.
