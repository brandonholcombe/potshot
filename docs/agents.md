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

1. **Screenshot rig**: PlayMode test (or `-executeMethod` runner) that loads
   a scene, drives scripted inputs N frames, calls
   `ScreenCapture.CaptureScreenshot` at checkpoints into `game/Logs/qa/`.
   Agents then Read the PNGs and judge composition/rendering. Note: PlayMode
   rendering needs a non-`-nographics` run; on this Mac an off-screen window
   is fine in batchmode-less CLI runs — see `scripts/` once the harness lands.
2. **Headless netcode runner**: launches the server build + N client
   processes with synthetic input scripts, asserts on end-state (positions,
   kills, pickups) written to JSON, then exits nonzero on violation.
3. **Feel numbers**: `docs/gameplay.md` feel targets are asserted in tests
   (top speed, time-to-kill) so blind tuning stays inside sane bounds.

## Human interface

- Blockers only → task file in `Agents/TODO/Human/` with: exact steps, why
  agents can't do it, expected minutes, and a pre-filled `HUMAN_INPUT.log`
  line for Brandon to append on completion.
- Playtest feedback arrives as free-form notes in `Agents/TODO/Backlog/` —
  triage into task docs.
