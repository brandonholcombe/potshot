# Gameplay

## Pitch

Top-down tank arena for ~10 friends. Rounds are short, deaths are funny,
shots are physical. The name is the design brief: a *potshot* is a casual,
opportunistic shot — the game should reward cheek, not grinding.

## Pillars

1. **Instant fun** — from launch to shooting a friend in <30 seconds. No
   loadout menus before the first match.
2. **Physical comedy** — shells are simulated projectiles with travel time,
   ricochets, and knockback. Missing is entertaining.
3. **Readable chaos** — top-down camera, chunky silhouettes, at most ~10
   tanks + projectiles on screen; a spectator should follow the action.

## MVP scope (M1)

- Tank: hull + independent turret, WASD/left-stick hull, mouse/right-stick
  turret. Weighty acceleration, moderate top speed.
- Weapons (3–4): standard cannon (ricochet ×1), spread shot, mortar lob
  (area), machine gun (hitscan-feel, low damage). Pickups spawn in arena.
- One arena: walls, destructible cover, weapon spawn points, 10 spawn pads.
- Bots: simple seek-and-shoot, enough to make solo testing meaningful and to
  fill lobbies.
- Mode: FFA deathmatch, first to N kills or timed round.

## Feel targets (tune blind, verify by playtest)

- Tank top speed ~7 u/s, reach in ~0.4 s; turret turn ~360°/s.
  (Bumped from 6 after the 2026-08-04 playtest.)
- **Boost** (Shift/Space, held-trigger): 1.8× speed and accel for 1 s,
  4 s cooldown from activation. Bots boost to close distance.
- Cannon shell ~14 u/s, 1 ricochet, 2-hit kill; respawn in 2 s.
- **Self-damage is on**: your own shells hurt you after they ricochet, and
  your own mortar AoE always hurts you. Bank shots carry risk by design
  (playtest-confirmed, 2026-08-04).
- Screen shake + hit pause on kill: small but present.

## Later (M5+, friends feedback drives ordering)

Team mode, battle-royale shrink, more arenas, tank customization, killcam,
per-weapon upgrades (ShellShot's "upgradable arsenal" idea, our way).
