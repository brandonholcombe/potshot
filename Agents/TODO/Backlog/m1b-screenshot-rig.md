# M1b — Screenshot rig (agents see the game)

## Author: (assign on activation)
## Status: Backlog — first task of M1

Per the M0b review (m0b-unity-project-and-qa-harness-review.md): adopt a
Unity MCP bridge as a **time-boxed trial** at M1 start.

1. **Unity MCP**: evaluate Unity's official MCP server vs CoderGamester/
   mcp-unity; install the package, register the server via `claude mcp add`.
   The AGENT launches and owns the editor process (no human clicks — Prime
   Directive intact). Benefits: no 30–90 s batchmode cold start per
   iteration, live console/compile errors, scene queries.
   Constraints from the review:
   - batchmode `scripts/run-tests.sh` remains the merge gate — MCP is for
     iteration speed only;
   - the MCP-owned editor must release the project lock before batchmode
     test/build runs (kill or close it first; document the recipe in
     docs/agents.md).
   - If the trial doesn't clearly pay for itself, drop it and record why.
2. **Screenshot rig** (docs/agents.md "Seeing without eyes"): PlayMode
   capture path — a runner that loads a scene, steps scripted input, writes
   PNGs to game/Logs/qa/ for agents to Read. Needs a graphics-capable run:
   add a `scripts/unity-gfx.sh` variant without `-nographics` (windowed,
   auto-quit, no human interaction).
