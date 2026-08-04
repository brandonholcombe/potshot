# M0b — Unity project bring-up + test pipeline

## Author: fable-primary
## Status: Not Started

## Context

Unity license activated (HUMAN_INPUT.log entry 1). `game/` project created
headlessly via `-createProject` (Unity 6000.1.14f1). `linux-server` module
installed. This task makes the agent verify-loop real: batchmode tests green
end-to-end.

## Plan

1. **Packages** (`game/Packages/manifest.json`): add
   `com.unity.test-framework` (not in the createProject default set). No
   other additions — FishNet lands in M2, input decision in M1.
2. **Assembly layout** under `game/Assets/`:
   - `Scripts/Runtime/Potshot.Core.asmdef` — runtime assembly; seed with
     `GameVersion.cs` (static version string, used by the M2 handshake).
   - `Editor/Potshot.EditorTools.asmdef` — editor assembly; seed with
     `ProjectConfigurator.cs`: idempotent `Configure()` sets
     companyName=kodloki, productName=Potshot, bundle id, fixed timestep
     1/60, disables unused quality tiers. Run via
     `scripts/unity.sh -quit -executeMethod Potshot.EditorTools.ProjectConfigurator.Configure`.
   - `Tests/EditMode/Potshot.Tests.EditMode.asmdef` + a real assertion test
     on GameVersion format.
   - `Tests/PlayMode/Potshot.Tests.PlayMode.asmdef` + a trivial smoke test.
3. **Verify**: `scripts/run-tests.sh` runs both platforms green; commit the
   generated `.meta` files; `align.py check` stays green.
4. **Docs**: PROJECT_STATUS.md M0 checklist updates.

## Explicitly deferred

- Screenshot rig → M1a (needs something to look at; requires a non
  `-nographics` run, see docs/agents.md).
- Unity MCP bridge (editor-process control, console access, no cold starts)
  → decision recorded in this task's review cycle, implementation as an
  `m1a-unity-mcp` backlog task at M1 start. Batchmode suffices for M0b.

## Risks

- First batchmode runs recompile/import — allow 10 min timeouts, serialize
  Unity invocations (single project lock).
- `.meta` churn: only commit metas for files we add; `.gitignore` already
  excludes Library/Logs/UserSettings.
