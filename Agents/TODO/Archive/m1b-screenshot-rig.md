# M1b — Screenshot rig (agents see the game)

## Author: fable-primary
## Status: Complete (2026-08-04) — probe + play-mode capture verified by agent-read PNGs; merge gate green and Visual-excluded. Deviation from reviewed design: Visual asmdef is EditMode+EnterPlayMode (editor-only asmdefs are EditMode to UTF), capture is sync RT (no WaitForEndOfFrame)

Give agents visual verification: scripted runs that write PNGs to
`game/Logs/qa/`, which agents then Read and judge. Replaces human eyeballs
for "does it render / is the scene composed right" (docs/agents.md,
"Seeing without eyes").

## Design

Two capture paths, both zero-GUI-for-humans:

1. **Edit-mode render probe** (fast, no play mode): editor method
   `Potshot.EditorTools.QaScreenshots.RenderProbe` — opens a scene (from
   `-potshotScene <path>` arg, default all scenes under Assets/Scenes),
   renders each enabled Camera to a 1280x720 RenderTexture, ReadPixels →
   PNG at `game/Logs/qa/<scene>-<camera>.png`. Rendering in pure batchmode
   is NOT guaranteed — so it runs via `unity-gfx.sh` (below). Use case:
   scene composition checks after SceneBuilder changes.
2. **PlayMode capture helper**: runtime class `Potshot.QaCapture`
   (Potshot.Core) with `IEnumerator Capture(string name)` — waits
   end-of-frame, `ScreenCapture.CaptureScreenshotAsTexture`, writes PNG to
   `game/Logs/qa/`. Used inside PlayMode tests for gameplay states. Visual
   tests live in a separate `Potshot.Tests.Visual` asmdef so the batchmode
   merge gate never runs them; they run via unity-gfx.sh only.

Supporting pieces:

- `scripts/unity-gfx.sh`: same wrapper as unity.sh but WITHOUT
  `-batchmode`/`-nographics` (windowed editor, auto-quits; requires
  logged-in GUI session — never over SSH; refuses to run if editor-daemon
  is up — same single-lock rule as run-tests.sh).
- First real use of `SceneBuilder`:
  `Potshot.EditorTools.SceneBuilder.BuildQaScene` creates
  `Assets/Scenes/QaProbe.unity` — ground plane, 3 colored primitives,
  directional light, top-down camera at (0,20,0) looking down. Idempotent.
- Verification of the whole rig: build QA scene → RenderProbe → agent Reads
  the PNG and confirms content is visible (non-uniform image); PlayMode
  Visual test captures during simulation → agent Reads PNG.
- docs/agents.md: update "Seeing without eyes" with the real commands and
  the Visual-assembly exclusion mechanism.

## Risks

- The editor window briefly appears on the Mac during gfx runs —
  acceptable (no human interaction needed).
- PNGs are for agents to judge semantically, not pixel-exact asserts — no
  golden-image brittleness in CI.
- run-tests.sh merge gate must keep excluding Visual tests as test count
  grows (assembly separation makes this structural, not filter-string).
