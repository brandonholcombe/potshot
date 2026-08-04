# Review — m1e-pickups-ammo.md

## Reviewer: sonnet-reviewer
Task: `Agents/TODO/Active/m1e-pickups-ammo.md` (Author: fable-primary)

## Verdict: approve-with-changes

Right slice, right conventions (spec table stays the source of truth, ammo is
controller state not SO state, plain-method mutation for M2). The trigger
mechanics in the Risks section are physically correct. But the Equip cooldown
reset becomes an exploit the moment weapons arrive mid-fight (1), the DevHud
lands on target dummies too (2), and the test plan can't work without a spec
injection API on WeaponPickup (3). Fix 1–4 before implementing.

## Findings

1. **BLOCKER — `Equip` resets `_cooldown = 0`; with pickups this is a
   fire-rate exploit.** Today the reset is dev-hotkey-only; after this slice,
   driving over a pad (or depleting to auto-`Equip(defaultWeapon)`) zeroes any
   pending cooldown — dump 8 spread shots, deplete, and the auto-equipped
   cannon fires the same tick; or pad-swap to skip mortar's 1.5 s. Change to
   `_cooldown = Mathf.Min(_cooldown, spec.fireCooldown)` (preserve remaining,
   clamped to the new weapon's cadence). Also pin decrement semantics: ammo
   decrements once inside the cooldown-gated `Tick` branch (so a spread volley
   costs 1 and direct `Fire()` calls in tests don't consume), and depletion
   check runs AFTER the depleting shot spawns.

2. **BLOCKER — DevHud on the prefab means 4 overlapping HUDs.** SceneBuilder's
   target dummies instantiate the same prefab; it strips `PlayerTankInput` and
   `PlaytestHotkeys` but the task adds DevHud to the prefab with no strip.
   Either add DevHud to the SceneBuilder dummy-strip list, or (better, covers
   future scenes) DevHud disables itself in Awake when no `PlayerTankInput`
   sibling exists. Same guard incidentally silences it in PlayMode tests.

3. **BLOCKER — WeaponPickup needs a spec-injection API and the pad needs a
   stated prefab source; tests cannot use DevArena.** Per m1c finding 3 the
   PlayMode asmdef has no editor APIs and DevArena isn't in build settings —
   the pad test must `AddComponent<PickupPad>()` on a code-built env. So:
   (a) WeaponPickup gets a `static Spawn(prefab, pos, WeaponSpec)` (Projectile
   pattern) so the equip test spawns a KNOWN weapon, not a random one;
   (b) PickupPad states where its pickup prefab and candidate spec list come
   from (public fields wired by SceneBuilder; tests wire via Resources.Load);
   (c) pad "empty" detection = destroyed-reference null check in FixedUpdate,
   timer restarts on empty — say so, it's what the 0.5 s test measures.

4. **Two tanks, one pickup — double equip.** Both OnTriggerEnter callbacks run
   before `Destroy` takes effect at end of frame; two tanks entering the same
   physics step both equip. One `bool _consumed` guard in the handler. Cheap,
   and the M2 server port inherits the correct semantics.

5. **Trigger mechanics — confirmed sound, two notes.** Static trigger collider
   (no Rigidbody) vs the tank's dynamic Rigidbody+BoxCollider: Unity fires
   OnTriggerEnter on BOTH sides; handling on the pickup side is the right
   choice (pickup owns its lifecycle). Notes: (a) resolve the tank via
   `other.GetComponentInParent<TankController>()`, not GetComponent, so a
   future compound collider doesn't break it; (b) the "slow spin" must rotate
   a collider-less visual child, NOT the GameObject carrying the trigger —
   rotating a static collider every frame forces per-frame physics re-sync;
   keep the trigger box axis-aligned and stationary.

6. **Projectiles vs pads — handler check is sufficient; no deflection risk.**
   Shells (layer 8, non-trigger) WILL raise OnTriggerEnter on a Default-layer
   pickup; the TankController check discards them correctly. Triggers generate
   no collision response, so no ricochet/deflection concern. The dedicated
   pickup-layer + `IgnoreLayerCollision(8, pickup)` alternative is cleaner but
   not worth the layer budget for M1 — handler logic approved as "simplest".
   Related debt to land NOW: `Projectile.Impact`'s OverlapSphere still omits
   `QueryTriggerInteraction.Ignore` (m1d 5c, never implemented) — pickup
   triggers will start appearing in mortar blast queries; harmless today (no
   Damageable) but add the argument in this slice.

7. **Death/respawn ammo reset — assign it a home so m1f can't forget.** Add a
   `ResetToDefault()` (Equip(defaultWeapon)) on WeaponController and a line in
   the task doc: the m1f respawn path MUST call it. Do not hide the reset in
   OnEnable — implicit resets will fight M2 server-driven respawn ordering.

8. **PlaytestHotkeys now bypass the pickup economy — keep, but document.**
   Free full-ammo equips are the right dev affordance for tuning; removing
   them this slice would hurt playtests. Add a comment on the class and a note
   in the task: M2's server-authoritative equip path removes/strips them
   (along with PlayerTankInput) from server builds — one consolidated strip
   list, tracked in the M2 task.

9. **Minor.** (a) Random.Range pad selection: task already carries the M2
   server-side/determinism note — consistent with m1c/m1d convention, fine.
   (b) DevHud OnGUI in a headless `-batchmode -nographics` server is never
   invoked — no `#if UNITY_SERVER` needed; the finding-8 strip list covers it.
   (c) Test teleporting the tank onto a pad: teleport-into-overlap DOES raise
   OnTriggerEnter on the next physics step (tank RB stays awake — TankMotor
   writes velocity every tick); prefer `rb.position = padPos` then two
   `WaitForFixedUpdate`s before asserting. (d) `ammo == 0` = infinite sentinel
   is fine; ensure the decrement path skips when `current.ammo == 0` rather
   than counting down from 0. (e) A tank parked on a pad auto-equips the
   instant the pad respawns (spawn-into-overlap fires next step) — acceptable,
   arguably intended; note it so nobody files it as a bug.
