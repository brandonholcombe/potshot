# M1f — Bots, FFA scoring, respawns (M1 slice d — closes M1)

## Author: fable-primary
## Status: Not Started

The game loop: everything dies, respawns, and counts. Replaces DevArena's
inert dummies with bots that fight back.

## Design

- **FfaGameMode** (scene singleton): discovers all TankControllers at
  Start (and exposes `Register(tank)` for runtime spawns/tests).
  - Subscribes to each tank's `Damageable.Died`: killer +1 kill (self-kill
    or environment: -1, floor at 0? No — allow negatives, potshot honesty),
    victim scheduled for respawn after 2 s (docs/gameplay.md).
  - Respawn: reactivate at the farthest spawn point from living enemies
    (simple max-min-distance over fixed spawn points), reset health
    (needs `Damageable.ResetHealth()` — new), `WeaponController.
    ResetToDefault()` (staged in m1e), zero velocity.
  - Round: first to 10 kills or 180 s; on end, `RoundOver` event with
    standings, freeze inputs 5 s (set a `RoundActive` flag TankController
    checks), reset scores + full respawn, go again. Round state readable
    for HUD/tests.
  - All state mutation in plain methods — M2 server authority reuses.
- **Damageable**: add `ResetHealth()` (+ reactivation support: death
  deactivates, game mode reactivates).
- **BotBrain** (MonoBehaviour implementing ITankInput, claims InputSource
  in OnEnable if none — same pattern as PlayerTankInput):
  - Perception each ~0.2 s (staggered): nearest living enemy tank,
    nearest pickup within 12 u.
  - Movement: seek target to preferred range 8–12 u (approach if farther,
    strafe-orbit inside); detour to pickup if closer than enemy and bot
    has default weapon; forward raycast (3 u) wall avoidance → steer away.
  - Combat: aim at enemy position + simple velocity lead (distance/
    projectileSpeed); fire when line-of-sight raycast hits the enemy and
    range < 16 u. Mortar: fire at range 8–20 regardless of LoS (it lobs).
  - Difficulty knobs on a `BotSpec` ScriptableObject (aim jitter degrees,
    reaction interval, preferred range) — one default asset via factory.
- **SceneBuilder.BuildDevArena**: replace 3 dummies with 3 BotBrain tanks
  (PlayerTankInput/PlaytestHotkeys/DevHud stripped, BotBrain added);
  8 spawn points ring (radius ~15); FfaGameMode object wired with spawn
  points. Add 2–3 destructible-free cover blocks near center (LoS breaks
  make bot fights and ricochets interesting).
- **DevHud**: add score lines (from FfaGameMode standings) + round timer.

## Verification (PlayMode, primitives-only env per convention)

- Kill scoring: scripted kill → killer credited; self-kill (own ricochet)
  → -1.
- Respawn: dead tank reactivates after ~2 s with full health, default
  weapon, at a registered spawn point.
- Round end: reaching kill target fires RoundOver with correct winner;
  scores reset after intermission.
- Bots: in a bare arena, a bot (a) moves toward a target dummy, (b) fires
  when in range with LoS, (c) kills it eventually (generous timeout);
  bot vs wall: forward raycast steers it off (position keeps changing,
  no permanent wall-grind — assert displacement over time).
- Visual: DevArena probe (bots + cover + pads); play-mode capture at ~5 s
  of bot combat — agent Reads for tanks spread out and shells in flight.

## Risks

- Bot quality is a feel problem — keep the state machine tiny and tune
  in the M5 loop. This slice's bar: bots fight, die, respawn, occasionally
  bank a ricochet; NOT competitive AI.
- Died event ordering with SetActive(false): FfaGameMode must not
  reactivate within the same physics callback — respawn via game-mode
  timer (FixedUpdate), never inside the event handler.
- TankController.Update turret rotation with InputSource swapped mid-play
  (respawn) — ResetToDefault + input persistence verified in tests.
