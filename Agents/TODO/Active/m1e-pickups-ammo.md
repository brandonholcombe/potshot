# M1e — Weapon pickups + ammo (M1 slice c)

## Author: fable-primary
## Status: Complete (2026-08-04) — gate 27/27 green (10 EditMode, 17 PlayMode); pads verified in probe

Weapon economy from docs/gameplay.md: cannon is the infinite default;
spread/mortar/mg are limited-ammo pickups that spawn on arena pads.

## Design

- **WeaponSpec** gains `int ammo` (0 = infinite). Table: cannon 0,
  spread 8, mortar 5, mg 40. One trigger pull = 1 ammo (a spread volley
  is one pull).
- **WeaponController**: `defaultWeapon` (cannon) field wired by
  PrefabFactory; `AmmoLeft` tracked; `Equip(spec)` reloads to spec.ammo;
  on depletion auto-Equip(defaultWeapon). Public `(WeaponSpec, int)`
  state readable for HUD/tests. All mutation stays in plain methods —
  M2 server authority calls the same paths.
- **WeaponPickup** (new prefab): floating box, trigger collider (layer
  default), material = weapon's projectile material, slow spin (visual
  Update). OnTriggerEnter with a TankController → Equip + despawn.
- **PickupPad** component (scene object): holds spawn point + respawn
  interval (10 s); spawns a random non-default weapon pickup when empty
  (deterministic seed not required in M1; M2 makes this server-side).
  Timer runs in FixedUpdate.
- **DevHud** (runtime, OnGUI IMGUI, dev-only aesthetics): weapon name,
  ammo ("∞" for cannon), health. On the player tank. IMGUI is fine here;
  real UI is an M5 concern.
- **SceneBuilder.BuildDevArena**: 4 PickupPads at (±12, 0, ±12).
- **PrefabFactory**: `CreateWeaponPickupPrefab()` → Resources/Prefabs/
  WeaponPickup.prefab; tank prefab gains DevHud; WeaponController gets
  defaultWeapon wired.

## Verification (PlayMode, primitives-only env)

- Driving over a pickup equips that weapon with full ammo.
- Firing decrements ammo; spread volley costs exactly 1.
- Ammo exhaustion reverts to cannon (and cannon never depletes).
- Empty pad respawns a pickup after its interval (fast-interval pad in
  the test, e.g. 0.5 s).
- EditMode: ammo table values (cannon 0 / spread 8 / mortar 5 / mg 40).
- Visual: DevArena probe shows pads with pickups present.

## Risks

- Trigger vs tank collider: tank has a single root BoxCollider (not
  trigger) + Rigidbody — OnTriggerEnter fires on the pickup side; the
  pickup needs no Rigidbody (static trigger touched by moving RB).
- Pickup trigger must ignore projectiles (shells crossing a pad must not
  consume it): check for TankController in the handler, and put pickups
  on the Projectile-ignored path only via handler logic (simplest).
