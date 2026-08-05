# Net/UX — Match-start transition sweep (fast bullets, camera, the rest)

## Author: fable-primary
## Status: Complete (2026-08-04) — 6 fixes shipped (camera poll+snap, presence-gated bots, NT teleport, round-2 EventSystem, loading mask, AudioListeners); deployed b2ad88e via new strict deploy.sh

Brandon (2026-08-04): bullets far too fast at match start; weird camera
at match start. Plus a process correction: the lobby slice shipped a
CLUSTER of transition defects (ghost game, dead overlay buttons, now
these) — this task sweeps the transition holistically under full review
instead of continuing one-symptom-at-a-time fixes.

## Suspected mechanisms (reviewer: verify in 4.7.2 source)

1. **Tick catch-up visual burst**: after the match scene load the client
   re-syncs by running up to _maximumFrameTicks (3) per frame — for the
   debt window, per-tick simulation (tracer/bullet motion, tank steps)
   renders at up to 3x speed. Verify precisely: with tick dropping
   enabled (our default), how much debt is SIMULATED vs DROPPED after a
   1-3 s load stall? How long is the fast-forward window? Sanctioned
   mitigation candidates (pick with citations): brief input/sim freeze
   at match start ("3-2-1-GO" style — we already have Countdown, but it
   runs BEFORE the load; consider a short post-load freeze phase),
   TimeManager settings, or accept-if-window-is-tiny with evidence.
2. **Camera handoff gap**: map scene cameras target the baked offline
   tank; CleanOfflineActors (ghost fix) destroys it → camera frozen at
   scene position → when the player's TankNet spawns, retarget +
   exponential smoothing = a swoop across the map. Fix: CameraFollow
   gains SnapTo (teleport to target+offset); NetworkTank retarget snaps.
   Also audit: LobbyState phase transitions leave any frame where NO
   camera exists or two AudioListeners fight (scene swap windows).
3. **Full transition audit** (the sweep): enumerate every actor/system
   across Warmup→Countdown→Match and Match→PostMatch→Warmup boundaries:
   DevHud (dies with warmup tank — flicker?), pause menu open across a
   swap, pre-join status panel left open, NetDevHotkeys status line,
   projectiles/tracers alive across a swap (despawned? orphaned?),
   PickupPads/FfaGameMode re-disabled (covered), AudioListener count.
   Deliver a checklist with verdicts, fix what's broken.
4. **Tests**: extend the lifecycle test — after match load: camera
   within X units of the owned tank within N frames (catches both
   frozen-camera and cross-map swoop); no orphaned projectiles across
   the PostMatch→Warmup swap. Catch-up window: if a freeze mitigation
   lands, assert inputs/sim gated during it.
5. Deploy per change scope (camera/UI = client-only; any
   LobbyState/timing change = both sides).

## Risks

- Timing changes interact with prediction — same rule as the tick-burst
  investigation: no invented timing hacks; FishNet's own design with
  citations or nothing.
- The camera snap must not fight the smoother (snap the CAMERA, not the
  graphical).
