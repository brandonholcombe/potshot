# Review — m0b-unity-project-and-qa-harness.md

## Reviewer: sonnet-reviewer
Task: `Agents/TODO/Active/m0b-unity-project-and-qa-harness.md` (Author: fable-primary)

## Verdict: approve-with-changes

The plan is structurally sound and correctly scoped (no FishNet, no input decision,
screenshot rig deferred). The changes below are compile-blockers or churn hazards,
not restructuring.

## Findings

1. **test-framework addition: correct.** Verified `com.unity.test-framework` is NOT
   in `game/Packages/manifest.json` (the `-createProject` set is modules-only plus
   `com.unity.multiplayer.center`), so adding it is required for `-runTests`. Let
   Unity resolve the verified version for 6000.1 (1.4.x line; pulls in
   `com.unity.ext.nunit` automatically), then commit the updated
   `Packages/packages-lock.json` in the same change — the plan doesn't mention the
   lock file. Optional: drop `com.unity.multiplayer.center` (GUI wizard, useless to
   agents, adds import time).

2. **Test asmdefs as described will not compile.** The plan omits every required
   asmdef field. Both test asmdefs need `"overrideReferences": true`,
   `"precompiledReferences": ["nunit.framework.dll"]`,
   `"defineConstraints": ["UNITY_INCLUDE_TESTS"]`, and a reference to
   `Potshot.Core` (the GameVersion test can't see it otherwise). EditMode also
   needs `"references": ["UnityEngine.TestRunner", "UnityEditor.TestRunner"]` and
   `"includePlatforms": ["Editor"]`. PlayMode should reference only
   `UnityEngine.TestRunner` (not the Editor runner) and leave platforms open.

3. **`Potshot.EditorTools.asmdef` must set `"includePlatforms": ["Editor"]`.**
   Living under `Assets/Editor/` is not sufficient once an asmdef exists — without
   the platform constraint the UnityEditor references break the M3 headless player
   build.

4. **Batchmode flags: verified OK.** `run-tests.sh` does not pass `-quit`
   (correct — `-quit` aborts `-runTests` before results are written), and the
   ProjectConfigurator invocation correctly includes `-quit` for `-executeMethod`.
   The wrapper's always-on `-nographics` is fine for the planned trivial PlayMode
   smoke test but will mask anything rendering-dependent — keep PlayMode asserts
   logic-only until the M1a screenshot rig introduces a graphics-capable path.

5. **ProjectConfigurator idempotency: mostly free, one trap.** PlayerSettings
   setters and fixed timestep are naturally idempotent, but "disables unused
   quality tiers" is not — there is no public API to delete quality levels, so
   this means SerializedObject surgery on `QualitySettings.asset`, and index-based
   deletion is order-dependent across re-runs. Either match tiers by name, or
   descope to "set the active tier per platform" (sufficient for M0b). `Configure()`
   should end with `AssetDatabase.SaveAssets()` rather than relying on `-quit`
   shutdown, and log what it changed vs. skipped.

6. **.meta / gitignore: two concrete gaps.**
   - Every *folder* needs its `.meta` too (`Assets/Scripts/`, `Runtime/`,
     `Tests/EditMode/`, ...). "Only commit metas for files we add" undercounts;
     missing folder metas cause GUID regeneration churn. After the first import,
     add `game/Assets/` wholesale and grep for orphan metas.
   - Add `game/Assets/InitTestScene*` to `.gitignore`: a crashed PlayMode run
     leaves the runner's temp scene (+ meta) in Assets root. Also add lowercase
     `game/obj/` — the ignore has `game/Obj/` but IDE packages generate lowercase,
     and git is case-sensitive even where the filesystem isn't.

7. **Minor, run-tests.sh** (pre-existing, not the plan's fault): UTF exit code 2 =
   tests failed, 3 = run error; the script folds both into 1. Fine for now; worth
   distinguishing when CI wants "compile error" vs "test failure" signals.

## MCP bridge decision (pending, recorded here)

**Recommendation: defer to M1 start, then adopt as a time-boxed trial
(`m1a-unity-mcp`), keeping batchmode scripts as the verification truth.**
Rationale: M1's tight tune-and-look loop pays a 30–90s domain-reload tax on every
cold batchmode start, and live console access beats log scraping — while the
zero-human-GUI constraint is satisfied because the *agent* launches and owns the
editor process (a window may exist; no human ever clicks it). Guardrails: the MCP
editor holds the single project lock, so it must be shut down before
`run-tests.sh`/`build-server.sh`; `run-tests.sh` green in batchmode remains the
merge gate regardless of how code was authored. Prefer Unity's official MCP if it
supports 6000.1 headless launch at trial time; CoderGamester/mcp-unity as fallback.
Adopting it *now* (M0b) would add moving parts before there is anything to iterate
on — batchmode suffices for this task, as the plan says.
