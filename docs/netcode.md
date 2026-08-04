# Netcode

## Stack

- **FishNet** (free tier), transport **Tugboat** (UDP) for dev/self-hosted;
  **FishySteamworks** (Steam Datagram Relay) added at M6. Game code must stay
  transport-agnostic — no raw IP/connection logic outside a single
  `ConnectionManager`.
- Install method: FishNet ships as a `.unitypackage` on GitHub releases.
  A `.unitypackage` is a gzipped tar — agents can extract it into
  `game/Assets/` without the editor GUI, or run
  `unity.sh -importPackage <file>` in batchmode. Prefer the batchmode import
  so `.meta` files come from Unity itself.

## Model

- **Server-authoritative**: server owns tank state, projectiles, damage,
  pickups. Clients send inputs only.
- **Client-side prediction + reconciliation** (FishNet prediction v2) on the
  local tank; misprediction snaps are smoothed over ~100 ms.
- **Snapshot interpolation** for remote tanks/projectiles, ~100 ms buffer.
- **Tick rate 30 Hz**. At 10 players this is ~10–20 KB/s per client — headroom
  is enormous; do not micro-optimize bandwidth before profiling.
- Projectiles: server-simulated; the firing client spawns an immediate local
  visual-only shell that reconciles to the server one.
- Hit registration: server-side, no lag compensation initially (friends on
  <100 ms; revisit if playtests complain about "I dodged that").

## Version handshake

Server embeds a build version (git short SHA + counter). Clients present it
on connect; mismatch → rejected with a "get the new build" message. This is
mandatory from M2 onward — it prevents every "why can't I connect" session.

## Testing without humans

- Headless server + N scripted headless clients driven by recorded/synthetic
  input; assert on authoritative positions, kill counts, pickup state.
- Latency/loss simulation via FishNet's built-in latency simulator; test at
  0 ms, 80 ms, 150 ms + 2% loss.
- See `docs/agents.md` for the batchmode recipes.
