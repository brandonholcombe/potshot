# Net — Instant fire feedback (local tracer + owned-shell hiding)

## Author: fable-primary
## Status: Not Started

Brandon (2026-08-04): "terrible delay from shooting… the bullet does not
even come out of the barrel, but where the barrel was 1/2 second ago."
Cause: shells are 100% server-authoritative (m2c's explicit deferral) —
click → RTT/2 → server tick fires from ITS (lagged) muzzle → RTT/2 back
→ +interpolation buffer. Late AND displaced while moving.

## Design

1. **Owner-local visual tracer**: in NetworkTank's replicate, on the
   OWNER (IsOwner && !IsServerStarted) and ONLY on fresh created ticks
   (same ContainsCreated/!IsReplayed guard as server fire — replays must
   NOT spawn tracers), run `NetworkWeapon.OwnerVisualTick(sample, dt)`:
   a local cooldown mirroring the synced weapon spec; on fire, invoke
   the SAME WeaponController.Fire path with a spawn override producing
   a VISUAL-ONLY offline Projectile (damage 0, aoe 0 — ricochets and
   despawn behave normally for believability) from the CURRENT muzzle —
   which sits under the smoothed Graphical child, i.e. exactly the
   on-screen barrel.
2. **Hide the owner's authoritative shells**: server spawns ProjectileNet
   with the FIRER'S owner connection (ServerManager.Spawn(go, ownerConn);
   bots stay ownerless). NetworkProjectile.OnStartClient: if IsOwner →
   disable renderer (client copy is already sim-stripped). Remote players
   see the authoritative shell; the firer sees only their tracer.
3. **Cooldown mirror drift**: local cooldown may rarely disagree with the
   server's (mid-equip races) → a tracer without a real shell or vice
   versa; acceptable at friend-latency, ammo HUD stays server-synced.
4. **Mortar**: identical local ballistic solve (same code path) — the
   tracer arcs correctly.
5. **Tests**: PlayMode (compat flow, host mode nuance: host IS server —
   tracer path requires a pure-client; in-process host can't produce
   IsOwner && !IsServerStarted… verify what's assertable: unit-level
   OwnerVisualTick spawns a damage-0 projectile from a mocked context;
   owned-shell hiding assertable server-side by checking spawn ownership;
   renderer-hiding logic unit-testable via direct component call).
   Live E2E remains Brandon's eyes.
6. Deploy both sides (spawn-ownership change is server-side).

## Risks

- Replay-guard omission would machine-gun tracers during reconciles —
  the ContainsCreated guard is the critical line (reviewer verify the
  owner-side replicate state semantics in 4.7.2: which states does the
  OWNER see on fresh vs replayed ticks).
- Owner-hidden shells still collide server-side (unchanged); client
  visual-only tracer must ignore pickups/triggers (existing Projectile
  handles — verify trigger interaction).
