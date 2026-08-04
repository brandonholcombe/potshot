# Netcode

## Stack

- **FishNet** (free tier), transport **Tugboat** (UDP) for dev/self-hosted;
  **FishySteamworks** (Steam Datagram Relay) added at M6. Game code must stay
  transport-agnostic — no raw IP/connection logic outside a single
  `ConnectionManager`.
- **Installed (M2a): FishNet 4.7.2 via UPM git reference** —
  `"com.firstgeargames.fishnet": "https://github.com/FirstGearGames/FishNet.git?path=Assets/FishNet#4.7.2"`.
  No vendored source in the repo (FishNet's custom license does not
  clearly permit public redistribution — M2a review); only manifest +
  packages-lock (exact commit) are committed. `Potshot.Net` asmdef
  references `FishNet.Runtime`. NetworkHub prefab (NetworkManager +
  Tugboat:7777 + VersionAuthenticator + DefaultPrefabObjects) generated
  by NetworkFactory. Connect via NetBootstrap: `-potshotServer` /
  `-potshotClient <host>` CLI, or H (host) / J (join localhost) dev keys.

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
