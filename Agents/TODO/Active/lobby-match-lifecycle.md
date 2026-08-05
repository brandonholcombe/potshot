# Lobby — server-hosted lobby + match lifecycle

## Author: fable-primary
## Status: Not Started

Brandon (2026-08-04, AskUserQuestion): SINGLE lobby per server; WARMUP
ARENA while waiting (drive/shoot, no scoring); leader controls map +
bots + kill target + start. Replaces connect-straight-into-combat.

## Server state machine (LobbyState, spawned singleton NetworkObject)

States: `Warmup → Countdown(3 s) → Match → PostMatch(10 s) → Warmup`.

- SyncVars: state, selectedMap, botCount (0-6), killTarget (5-25),
  countdownRemaining; leaderId (first-connected ClientId; migrates to
  next-lowest on disconnect). Player roster comes from NetFfaState-style
  Names/NameSync (already synced).
- Leader commands as client→server broadcasts, server validates sender
  IS leader: SetMap / SetBots / SetKillTarget / StartMatch. Non-leader
  commands ignored (and logged).
- **Warmup**: server loads a new generated `Lobby` scene
  (SceneBuilder.BuildLobbyScene: ~22x22 pen, walls, no pads, no
  FfaGameMode). Players spawn on join and can drive/shoot; tanks are
  INVULNERABLE (server-side Damageable guard flag) — fun without
  bookkeeping. No bots in warmup.
- **StartMatch**: Countdown syncs; at 0 the server despawns all tanks,
  loads the selected map as a GLOBAL scene (FishNet SceneManager,
  ReplaceScenes — reviewer: verify SceneLoadData/ReplaceScenes API in
  4.7.2 and client auto-load flow), spawns NetFfaState (with leader's
  killTarget) + bots (leader's count) + player tanks.
- **Match end** (NetFfaState kill target hit): PostMatch — inputs
  frozen, standings overlay, 10 s; then global-load Lobby, despawn
  match objects, back to Warmup.

## Client flow changes

- Play Online: connect from the MENU scene; the server's global scene
  load replaces local scenes (menu included — verify ReplaceScenes.All
  semantics vs DDOL objects: hub, PauseMenuController survive).
  NetBootstrap no longer preloads DevArena for online (-potshotClient
  E2E path adjusts; server build boot scene + immediate Lobby global
  load at server start — verify sequencing).
- **LobbyOverlay UI** (UIFactory-generated, LobbyState-driven): player
  list with leader crown; leader panel (map cycle across build maps,
  bots -/+, kill target -/+, big START) vs "Waiting for <leader>…";
  countdown numerals; PostMatch standings list + "returning to lobby".
- Host mode gets the same flow (host IS leader).
- Offline flow completely untouched.

## Spawning rework

PlayerSpawner: joins during Warmup/Match spawn appropriately (warmup pen
vs mid-match join into the running map as a normal combatant); bots only
at match start per leader count; NetFfaState becomes MATCH-SCOPED
(spawned at match start, despawned at PostMatch end — Track/respawn
logic unchanged within a match).

## Tests

In-process host lifecycle: first client is leader; non-leader SetMap
ignored; StartMatch → countdown → active scene == selected map, bots ==
setting, NetFfaState alive with killTarget; scripted kills to target →
PostMatch → auto-return to Lobby scene with tanks respawned in pen;
leader migration when leader disconnects (needs 2 in-process
connections? single client + host-client... reviewer: judge what's
testable in-process, prescribe). Warmup invulnerability: TakeDamage in
warmup leaves health full. Offline suite untouched.

## Deploy

Server + client rebuild, rollout, live E2E: connect → expect Lobby
scene + warmup spawn (log lines), not instant DevArena combat.

## Risks

- FishNet global scene management is the least-exercised API surface we
  have — the reviewer's source verification is the core of this review
  (SceneLoadData, ReplaceScenes, start-scene interplay,
  OnClientLoadedStartScenes timing vs our spawn hooks, physics scene
  handling on scene swap with active rigidbodies).
- Mid-match joins + late name broadcasts (existing tolerance patterns).
- The -potshotClient headless E2E and NetMoveTests/NetCombatTests all
  assume immediate spawn-on-connect — they must adapt to the lifecycle
  (helper: LobbyState test hook to jump straight to Match with defaults,
  or tests drive StartMatch as the leader).
