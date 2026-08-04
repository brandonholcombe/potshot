# Potshot — Agent Onboarding

**Potshot** is a fast-paced, top-down, online multiplayer tank arena game for a
small friends group (~10 players), built with Unity + FishNet, deployed as a
dedicated server on the kodloki LKE cluster (`tow-c1`), with a Steam release as
the end goal.

This project is an experiment in **agent-built software with minimal human
input**. Read this file fully before doing anything.

## Prime Directive: No Editor GUI Work

**Never create a task that requires a human to click in the Unity editor.**
All scene, prefab, import, and settings work is done via editor scripts
executed in batchmode:

```bash
scripts/unity.sh -executeMethod Potshot.EditorTools.SceneBuilder.BuildArena
```

If you believe GUI work is genuinely unavoidable, stop and document why in a
task under `Agents/TODO/Human/` — that directory is intentionally near-empty
and every entry is a defeat for the experiment.

Any human action (installs, logins, physical playtests) must be logged by the
human in `HUMAN_INPUT.log`. Agents append a requested entry template when
filing a Human task.

## Division of Labor

- **Agents**: all C# code, editor scripts, scenes/prefabs (generated), tests,
  Docker images, K8s manifests, DNS (Linode API), docs, git pushes, releases.
- **Human (Brandon)**: account logins (Unity, Steam), payments, and playing
  the game. Nothing else without a documented reason.

## Where Truth Lives

- `PROJECT_STATUS.md` — milestone tracker (M0–M6). Update when state changes.
- `ARCHITECTURE.md` — system overview.
- `docs/` — per-domain deep dives: `gameplay.md`, `netcode.md`,
  `deployment.md`, `steam.md`, `assets.md`, `agents.md` (batchmode playbook).
- `docs/assets.md` — **license ledger**: every imported asset gets a row
  (source, license, Steam-redistribution status) in the same change that adds
  it. Asset budget: <$100 total, CC0 preferred (Kenney first).

## Repo Layout

```
game/       Unity project (client + server, one project)
server/     Dockerfile + entrypoint for the headless Linux server build
K8s/        Kubernetes manifests (shine layout; ArgoCD watches GitHub)
scripts/    unity.sh, run-tests.sh, build-server.sh — the agent CLI loop
docs/       domain documentation
Agents/     TODO/Active, TODO/Backlog, TODO/Human, Review-reports
```

## Review Gate

A PreToolUse hook (`.claude/hooks/require-review.sh`) enforces review before
implementation. When task documents exist in `Agents/TODO/Active/`, edits to
production files (`game/Assets/`, `server/`, `K8s/`, `scripts/`) are blocked
until a review report exists in `Agents/Review-reports/`.

1. Create a task doc in `Agents/TODO/Active/` with `## Author: <your-name>`
   and `## Status: Not Started`, containing the implementation plan.
2. A **different agent** writes a review in `Agents/Review-reports/` with
   `## Reviewer: <their-name>`, referencing the task filename.
3. Implement. Mark `## Status: Complete` when done and verified.

Author and Reviewer must differ — self-reviews are denied by the hook.
Ad-hoc work with no active tasks is not gated. Milestone-sized work always
gets a task doc; pull the next one from `Agents/TODO/Backlog/`.

## Build & Test (the agent verify loop)

```bash
scripts/unity.sh <args>       # wrapper around the pinned Unity editor CLI
scripts/run-tests.sh          # EditMode + PlayMode tests, batchmode
scripts/build-server.sh       # headless Linux server build → server/build/
```

Unity 6000.1.14f1 at `/Applications/Unity/Hub/Editor/6000.1.14f1`. Never
open the Unity GUI; never ask the human to. Visual verification is done by
capturing screenshots in automated PlayMode runs and Reading the PNGs
(see `docs/agents.md`).

## Naming & IP

The game is **Potshot** — never "ShellShot", "ShellShock", or any shell-name
(kChamp Games' marks; we recreate mechanics, never branding). Images are
`bholcombe/potshot-*`, host is `potshot.kodloki.io`, K8s namespace `potshot`.

## Git

Dual remotes per kodloki convention: `github` (canonical — CI/ArgoCD watches
it) and `gitea` (haxley mirror). Push both; GitHub failure is fatal, Gitea
failure is a warning. PATs come from `~/.config/eloup-wizard/secrets.json` —
never echo or commit them.
