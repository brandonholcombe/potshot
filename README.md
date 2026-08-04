# Potshot

Fast-paced, top-down, online multiplayer tank arena for a small group of
friends. Take potshots at your pals. Unity + FishNet, dedicated server on the
kodloki LKE cluster, Steam release planned.

- **Status**: see `PROJECT_STATUS.md`
- **Architecture**: see `ARCHITECTURE.md`
- **Agent onboarding**: see `CLAUDE.md` (this project is built almost entirely
  by AI agents — human input is tracked in `HUMAN_INPUT.log`)
- **Server**: `potshot.kodloki.io:7777/udp` (once M4 lands)

## Quickstart (agents)

```bash
scripts/run-tests.sh          # batchmode EditMode + PlayMode tests
scripts/build-server.sh       # headless Linux server → server/build/
```

## Quickstart (humans)

Download the latest client build (link on the status page once live), launch,
enter the server address, shoot your friends.
