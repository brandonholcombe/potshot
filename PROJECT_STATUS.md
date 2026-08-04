# Potshot — Project Status

## Milestones

| # | Milestone | Status | Notes |
|---|-----------|--------|-------|
| M0 | Scaffold: repo, template, docs, hooks, Unity project, test pipeline | ✅ Complete (2026-08-04) | Screenshot rig + Unity MCP trial moved to M1a |
| M1a | Unity MCP bridge (trial) | ✅ Complete (2026-08-04) | mcp-unity 1.4.0; editor-daemon.sh; trial verdict due at M1 end. Screenshot rig → m1b |
| M1 | Core game offline: tank controller, projectiles, 3–4 weapons, 1 arena, bots | 📋 Backlog | The "is it fun" gate |
| M2 | FishNet: server-auth movement, prediction, networked weapons, 2-client test | 📋 Backlog | |
| M3 | Dedicated server: Linux headless build, Docker, version handshake | 📋 Backlog | Needs linux-server Unity module |
| M4 | Deploy: K8s manifests, hostPort UDP, DNS CronJob, status endpoint, ArgoCD | 📋 Backlog | `potshot.kodloki.io` live |
| M5 | Friends alpha: dev builds distributed, feedback loop, weapons/maps/modes | 📋 Backlog | |
| M6 | Steam: branding lock + vetting, Steamworks module, FishySteamworks, store page, release | 📋 Backlog | $100 fee; 4–6 wk process; human-heavy |

## M0 checklist

- [x] Repo scaffold (baseline template: CLAUDE.md, docs/, Agents/, K8s/, scripts/)
- [x] Review-gate hook ported from eloup (adapted paths)
- [x] Symbolic alignment from the haxley baseline template (`symbols/` + align.py + hook + CI); 5 symbols, all aligned
- [x] Unity 6000.1.14f1 present on build machine
- [x] Unity Hub installed (by agent, headless)
- [x] Unity license activated — Human task 001 (HUMAN_INPUT.log entry 1)
- [x] Linux Dedicated Server module installed (agent, headless Hub CLI)
- [x] Unity project created at `game/` headlessly; ProjectConfigurator applied (1/60 fixed timestep, kodloki/Potshot identity)
- [x] git init + github/gitea remotes + first push
- [x] Registered in kodloki `PROJECT_REGISTRY.md`
- [x] Test pipeline green end-to-end: `run-tests.sh` → EditMode 2/2, PlayMode 1/1 (M0b, reviewed by sonnet-reviewer)
- [ ] Screenshot rig + Unity MCP trial → M1a backlog task

## Experiment metric

Human input tracked in `HUMAN_INPUT.log`. Target: first human action after
setup is playing the deployed game.
