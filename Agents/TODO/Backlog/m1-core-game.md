# M1 — Core game, offline

## Author: (assign on activation)
## Status: Backlog (blocked on M0b)

The "is it fun" milestone. Scope is `docs/gameplay.md` MVP: tank controller
(hull+turret), 3–4 physics projectile weapons, one generated arena with
destructible cover and pickups, seek-and-shoot bots, FFA scoring.

Constraints:
- All scenes/prefabs via `SceneBuilder`/`PrefabFactory` editor scripts.
- Kenney Top-down Tanks Redux (CC0) for art — ledger rows in `docs/assets.md`
  in the same change.
- Feel targets from `docs/gameplay.md` asserted in PlayMode tests.
- Screenshot-rig captures checked into the review for visual sign-off.

Break into task docs per the review gate; suggested slices: (a) project
config + tank movement, (b) weapons/projectiles, (c) arena generation +
pickups, (d) bots + FFA scoring + game loop.
