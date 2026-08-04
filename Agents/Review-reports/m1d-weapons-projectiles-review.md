# Review — m1d-weapons-projectiles.md

## Reviewer: sonnet-reviewer
Task: `Agents/TODO/Active/m1d-weapons-projectiles.md` (Author: fable-primary)

## Verdict: approve-with-changes

Right slice, right shape (spec SOs from a data table, one configured
projectile prefab, primitives-only tests). But three physics mechanisms
as written will not work (1–3), and the fire path/direction is unpinned
exactly the way m1c had to pin TankMotor (4). Fix 1–5 before implementing.

## Findings

1. **BLOCKER — ricochet reflects the wrong velocity.** OnCollisionEnter
   fires AFTER the solver resolves the contact; with the specified
   bounce-free material, `rb.linearVelocity` there is the post-solve
   tangential remainder — reflecting it around the normal returns it
   unchanged (it's already perpendicular to the normal), so shells slide
   along walls instead of bouncing. Required: cache `rb.linearVelocity`
   each FixedUpdate (runs pre-simulation, so it holds the incoming
   velocity) and reflect the CACHED vector around
   `collision.GetContact(0).normal`, renormalized to spec speed
   (`collision.relativeVelocity` is an acceptable alternative). The Risks
   note "GetContact(0).normal — fine" misses this: the normal is fine,
   the velocity is not.

2. **BLOCKER — shooter immunity must be `Physics.IgnoreCollision`, not
   time.** A 0.2 s ignore window also skips walls and OTHER tanks — MG
   covers 6 u in 0.2 s, so point-blank shots pass through targets; and a
   check inside OnCollisionEnter is too late (the solver already
   deflected the shell off the firer). Required: at spawn,
   `Physics.IgnoreCollision(projCol, firerCol, true)` — permanent per
   shot is simplest (re-enable later only if self-ricochet kills are
   wanted) — plus a spawn point outside the firer's BoxCollider (muzzle
   at turret-local z≈1.7 clears the 1.1 half-extent hull; check against
   projectileScale radius).

3. **BLOCKER — projectile-vs-projectile collisions kill spread shot.**
   Five pellets spawn overlapping at one muzzle; with sphere colliders +
   Continuous CD they OnCollisionEnter each other on step 1 and hit the
   "else despawn" branch. Put projectiles on a dedicated physics layer
   with self-collision off in the layer matrix — configured via editor
   script (TagManager/Physics settings), never the GUI; pairwise
   IgnoreCollision among pellets is the fallback.

4. **BLOCKER — pin the fire path and fire direction for M2.** (a) Firing
   runs in FixedUpdate; cooldown accumulates `fixedDeltaTime`, never
   `Time.time` — MG-test determinism and M2 tick replay require it.
   (b) One sample per tick: TankController already samples in
   FixedUpdate; WeaponController must consume THAT sample (passed down or
   exposed), not call `ITankInput.Sample()` again — in M2 the sample IS
   the replicated payload and must be identical for Step and TryFire.
   (c) "Spawns at the Muzzle transform" + `TryFire(aimWorldPos)` leaves
   fire DIRECTION ambiguous. m1c ruled turret rotation cosmetic (never
   predicted), so muzzle-forward is not state the M2 server can use.
   Pick and document: direction = XZ-normalized `aimWorldPos − tank.pos`,
   spawn pos derived along it (Muzzle is visual reference only). If the
   360°/s turret rate should instead gate shots, that makes turret yaw
   authoritative in M2 and contradicts m1c — resolve the tension now.

5. **Mortar semantics — four gaps.** (a) Direct hit double-damages: the
   Damageable branch (60) plus AoE (60) on the same tank → 120. For
   `aoeRadius > 0`, skip direct damage; AoE handles all of it. (b) AoE on
   ANY impact (wall/ground/tank) is correct — state it; none on lifetime
   expiry. (c) OverlapSphere: `QueryTriggerInteraction.Ignore`, resolve
   via `GetComponentInParent<Damageable>`, dedupe per Damageable. Note
   IgnoreCollision does NOT mask OverlapSphere — the firer WILL take own
   mortar splash; keep (fits "physical comedy") but document, and place
   the test's firer outside the 2.5 u radius. (d) v=√(g·d) assumes launch
   height 0; firing from muzzle y≈0.75 lands ~0.75 u long — half the
   1.5 u test budget before 60 Hz discretization. Solve with the height
   term or aim-compensate. Sanity: v 4.4–15.7 u/s, flight 0.64–2.26 s —
   acceptable feel; 5 s lifetime clears it.

6. **Damageable death guards.** Five pellets can land in one physics
   step: `TakeDamage` must no-op once dead so `Died` fires exactly once.
   Raise `Died` BEFORE `SetActive(false)` (slice-d listeners need
   position). Deactivating the target inside a projectile's
   OnCollisionEnter is safe (callbacks run post-solve), and
   `Object.Destroy` on an inactive object in TearDown is fine.

7. **MG test asserts only an upper bound — zero shots passes "≤11".**
   Cooldown 0.1 s = exactly 6 fixed steps → 10–11 shots in 60 steps;
   assert `Is.InRange(9, 11)`. Cannon: assert ≈14 within 1–2% one
   FixedUpdate after spawn (no gravity/drag → exact).

8. **Conventions (m1c-binding).** (a) `projectileColor` as a Color forces
   runtime `renderer.material`, which instantiates and LEAKS a material
   per shot — WeaponFactory should create per-weapon material assets via
   MaterialFactory, referenced from WeaponSpec (or MaterialPropertyBlock).
   (b) WeaponSpec assets and the projectile's PhysicsMaterial must be
   get-or-create (GUID stability, m1c 5b). (c) Parent Muzzle under
   Turret, not Barrel — the barrel's non-uniform scale (0.2, 0.2, 1.4)
   distorts child placement; turret's 0.15 y-scale scales child local y.
   (d) Projectile Rigidbody: drag 0, interpolation Interpolate,
   `useGravity` per spec; 30 u/s = 0.5 u/step, Continuous CD correctly
   specified. (e) The impact scale-pop must be its own GameObject so
   despawning the projectile doesn't kill the effect.
