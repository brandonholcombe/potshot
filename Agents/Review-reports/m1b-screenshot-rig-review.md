# Review — m1b-screenshot-rig.md

## Reviewer: sonnet-reviewer
Task: `Agents/TODO/Active/m1b-screenshot-rig.md` (Author: fable-primary)

## Verdict: approve-with-changes

The two-path design (edit-mode render probe + PlayMode capture, both via a
graphics-capable wrapper) is the right shape. But the exclusion story for the
merge gate is factually wrong as written, and would hang `run-tests.sh` the day
the first Visual test lands. Fix findings 1–3 before implementing.

## Findings

1. **BLOCKER — "assembly separation" does NOT exclude Visual tests from the
   merge gate.** `scripts/run-tests.sh` invokes `-runTests -testPlatform
   PlayMode` with **no filter**; the Unity Test Framework runs every test
   assembly matching the platform. A new `Potshot.Tests.Visual` asmdef WOULD be
   picked up, and its `WaitForEndOfFrame` never fires under `-batchmode
   -nographics` → the capture coroutine stalls and the gate hangs until
   timeout. The Risks line "assembly separation makes this structural, not
   filter-string" is exactly backwards — the filter string is the mechanism.
   Required changes:
   - `run-tests.sh`: add `-assemblyNames "Potshot.Tests.EditMode"` /
     `"Potshot.Tests.PlayMode"` per platform (or one `-assemblyNames
     "Potshot.Tests.EditMode;Potshot.Tests.PlayMode"`).
   - `unity-gfx.sh` test path: `-assemblyNames "Potshot.Tests.Visual"`.
   - Defense in depth: Visual test `[SetUp]` calls
     `Assume.That(!Application.isBatchMode)` so an unfiltered batchmode run
     skips (Inconclusive) instead of hanging.

2. **Render pipeline: verified Built-in — probe design is valid, add a
   guard.** `game/Packages/manifest.json` and `packages-lock.json` contain no
   `com.unity.render-pipelines.universal` (modules-only createProject set), so
   `Camera.Render()` into a RenderTexture works. Correct sequence:
   `cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
   tex.ReadPixels(...); tex.Apply();` then restore `RenderTexture.active`,
   null `targetTexture`, release the RT, `EncodeToPNG` (imageconversion module
   is present). Under any SRP, `Camera.Render()` silently does nothing — add
   an early `GraphicsSettings.currentRenderPipeline == null` assert with a
   pointer to `RenderPipeline.SubmitRenderRequest`/`SingleCameraRequest` so a
   future URP migration fails loudly, not with black PNGs.

3. **`-executeMethod` + `-quit` in a windowed editor: works, but prefer
   `EditorApplication.Exit`.** Two hazards specific to non-batchmode: (a) a
   dirty scene at quit can raise a modal save prompt — a permanent hang with
   nobody to click. RenderProbe must open scenes read-only and never dirty
   them; (b) exit codes: on failure, throwing does exit nonzero, but calling
   `EditorApplication.Exit(0|1)` at method end is deterministic and bypasses
   any dialog. If using `Exit`, drop `-quit`. Either way `unity-gfx.sh` should
   wrap invocations in a hard `timeout` (e.g. 600s) as a backstop.

4. **`unity-gfx.sh` flags and guards.**
   - Never combine `-quit` with `-runTests` (m0b review, finding 4). Without
     `-batchmode`, a CLI-initiated `-runTests` still auto-exits the editor with
     code 0/2/3 — but pass `-testResults game/Logs/test-results/Visual.xml`
     and keep the timeout backstop in case UTF's auto-exit regresses.
   - Copy run-tests.sh's editor-daemon pidfile check verbatim (single lock).
   - GUI-session detection is feasible: require
     `[ "$(launchctl managername)" = "Aqua" ]` (SSH sessions report
     `Background`), plus a cheap `[ -z "${SSH_CONNECTION:-}" ]` reject.
     Fail fast with a message pointing at batchmode alternatives.

5. **PlayMode capture: sound, two nits.** `yield return new
   WaitForEndOfFrame()` then `CaptureScreenshotAsTexture` is the documented
   pattern for windowed editor runs; batchmode caveat correctly motivates the
   gfx-only routing (contingent on finding 1). Nits: capture size is the Game
   view size, not 1280x720 — log `Screen.width/height` into the PNG filename
   or alongside; `Destroy()` the returned Texture2D after encoding
   (leaks across multi-capture tests); `Directory.CreateDirectory` the
   `Logs/qa/` dir before writing.

6. **QaProbe scene loading needs an explicit mechanism — pick
   `LoadSceneInPlayMode`.** `SceneManager.LoadScene("QaProbe")` fails unless
   the scene is in EditorBuildSettings, and adding it there leaks a QA scene
   into the M3 server build. Instead: Visual tests call
   `EditorSceneManager.LoadSceneInPlayMode(path, new
   LoadSceneParameters(LoadSceneMode.Single))` — editor-only API, fine because
   Visual runs only in-editor; give `Potshot.Tests.Visual.asmdef`
   `includePlatforms: ["Editor"]` (plus the m0b test-asmdef boilerplate:
   overrideReferences, nunit.framework.dll, UNITY_INCLUDE_TESTS, TestRunner
   refs). `BuildQaScene` itself is fine headless: `NewScene(EmptyScene,
   Single)` → build objects → `SaveScene(scene, path)` +
   `AssetDatabase.SaveAssets()`; re-run overwrites → idempotent. Camera:
   `Quaternion.Euler(90,0,0)` at (0,20,0).

7. **Minor — `QaCapture` in `Potshot.Core` ships in the server build.**
   Harmless but pointless payload; move it into `Potshot.Tests.Visual` (its
   only consumer), or wrap in `#if UNITY_EDITOR || DEVELOPMENT_BUILD`.
