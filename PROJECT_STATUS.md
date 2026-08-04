# Potshot — Project Status

## Milestones

| # | Milestone | Status | Notes |
|---|-----------|--------|-------|
| M0 | Scaffold: repo, template, docs, hooks, Unity project, QA harness spec | 🚧 In progress | Blocked on Unity license (Human task 001) for project creation |
| M1 | Core game offline: tank controller, projectiles, 3–4 weapons, 1 arena, bots | 📋 Backlog | The "is it fun" gate |
| M2 | FishNet: server-auth movement, prediction, networked weapons, 2-client test | 📋 Backlog | |
| M3 | Dedicated server: Linux headless build, Docker, version handshake | 📋 Backlog | Needs linux-server Unity module |
| M4 | Deploy: K8s manifests, hostPort UDP, DNS CronJob, status endpoint, ArgoCD | 📋 Backlog | `potshot.kodloki.io` live |
| M5 | Friends alpha: dev builds distributed, feedback loop, weapons/maps/modes | 📋 Backlog | |
| M6 | Steam: branding lock + vetting, Steamworks module, FishySteamworks, store page, release | 📋 Backlog | $100 fee; 4–6 wk process; human-heavy |

## M0 checklist

- [x] Repo scaffold (baseline template: CLAUDE.md, docs/, Agents/, K8s/, scripts/)
- [x] Review-gate hook ported from eloup (adapted paths, no symbols system)
- [x] Unity 6000.1.14f1 present on build machine
- [x] Unity Hub installed (by agent, headless)
- [ ] Unity license activated — **Human task 001**
- [ ] Linux Dedicated Server module installed (agent, after license)
- [ ] Unity project created at `game/` (agent, after license)
- [ ] git init + github/gitea remotes + first push
- [ ] Registered in kodloki `PROJECT_REGISTRY.md`
- [ ] QA harness (screenshot rig + headless multi-client runner) — M0 deliverable, may slip to early M1

## Experiment metric

Human input tracked in `HUMAN_INPUT.log`. Target: first human action after
setup is playing the deployed game.
