# Review — m1f-bots-ffa-respawn.md

## Reviewer: sonnet-reviewer
Task: `Agents/TODO/Active/m1f-bots-ffa-respawn.md` (Author: fable-primary)

## Verdict: approve-with-changes

Right slice, right shape (plain-method mutation for M2, respawn via game-mode
FixedUpdate timer not inside the Died handler, ResetToDefault reused from m1e).
The scoring identity chain checks out against the code. But the respawn write
order will silently drop state (1), Register can double-subscribe (2), the
round constants are untestable as specified (3), and the input-freeze and bot
raycasts need pinning (4, 8–9). Fix 1–4 before implementing.

## Findings

1. **BLOCKER — respawn write order.** A deactivated GameObject's Rigidbody is
   removed from the physics scene; velocity/pose writes on it don't stick.
   Required order: (a) `ResetHealth()` while still inactive (plain property —
   activating first leaves `IsDead` true, and the guard in `TakeDamage` makes
   the tank an invulnerable ghost until reset); (b) set `transform.position/
   rotation` while inactive (body is re-created at that pose on activation —
   also avoids an interpolation sweep-frame from the corpse position);
   (c) `SetActive(true)`; (d) zero `linearVelocity` AND `angularVelocity`;
   (e) `weapon.ResetToDefault()`. `Damageable.Awake` runs once, so
   `ResetHealth()` is genuinely required — the task has it; pin this order in
   the task doc. `TankController._lastSample` staleness (old aim) is cosmetic —
   RotateTowards is rate-limited; ignore.

2. **BLOCKER — `Register` must be idempotent and destroyed-tank-safe.** Start
   discovery + a test (or future spawner) calling `Register(tank)` double-
   subscribes `Died` → double score per kill. Guard with a set. Also: tests
   Destroy tanks in TearDown while a scene-leaked game mode could hold them —
   every iteration over registered tanks (respawn scheduling, living-enemy
   query, standings) needs a null/destroyed skip. No static `Instance` — static
   state leaks across PlayMode tests (the m1d phantom-failure lesson); tests
   and DevHud find it via `FindFirstObjectByType`.

3. **BLOCKER — round/respawn constants must be public fields, or the round
   tests can't run.** First-to-10 with a hardwired 2 s respawn means the
   round-over test spends 20+ s waiting out corpse timers (victim must
   reactivate before it can die again — `TakeDamage` no-ops while dead).
   Expose `killTarget`, `roundSeconds`, `respawnDelay`, `intermissionSeconds`
   and `Transform[] spawnPoints` as public fields (SceneBuilder wires scene
   objects; tests wire empty GameObjects). Scripted-kill crediting is
   confirmed workable: `TakeDamage(1000f, killerGo)` is public and `Died`
   delivers `(victim, source)` — no projectiles needed; self-kill = pass the
   victim's own GameObject.

4. **Input freeze: per-tank field on TankController — not a static, not an
   InputSource swap.** A static leaks across tests (see 2); swapping
   InputSource needs per-tank save/restore and fights the OnEnable
   claim-if-null on respawn reactivation. Add `public bool InputFrozen` to
   TankController; FfaGameMode iterates registered tanks. Semantics when
   frozen: still Sample (or reuse last), then zero `Move` and `Fire` but KEEP
   `AimWorldPos`, and still run `TankMotor.Step` — an early return skips decel
   and the tank slides at top speed through the whole intermission.

5. **InputSource across SetActive cycles — verified correct, no change.**
   The property survives deactivation; on reactivation OnEnable's
   claim-if-null no-ops because `InputSource` is already the same component
   instance (PlayerTankInput and BotBrain alike), and test-injected
   ScriptedTankInput overrides survive respawn untouched. Note it in the task
   so nobody "fixes" it into an unconditional claim.

6. **Firer immunity does not survive the firer's death.** `Physics.
   IgnoreCollision` pair state is cleared when a collider deactivates — a
   shell in flight when its owner dies can hit the owner after respawn without
   ever ricocheting (5 s lifetime vs 2 s respawn), scoring −1. Cheapest fix
   fitting the design: on death, FfaGameMode ignores it (document as potshot
   honesty) or destroys the dead tank's in-flight projectiles — pick one in
   the task. Also: the Died handler must tolerate `source` being null or a
   destroyed GameObject (unregistered source → environment, −1).

7. **Killer identity — verified.** `Projectile` passes `_firer` on both the
   direct path (`OnCollisionEnter`) and the AoE path (`Impact`), `_firer` is
   the tank root (same GameObject as Damageable), so self-kill is exactly
   `source == victim.gameObject`. A killer that died in the same tick is only
   deactivated, never destroyed — reference lookup still credits correctly.

8. **BotBrain: run the perception timer inside `Sample()`,** accumulating
   `Time.fixedDeltaTime` (Sample is called exactly once per tick by
   TankController.FixedUpdate; keeping ALL bot logic inside Sample keeps it
   tick-driven for M2 server-side bots). Stagger via a random initial phase.
   BotBrain must NOT depend on FfaGameMode for targets — the bare-arena bot
   tests spawn no game mode; use `FindObjectsByType<TankController>` on the
   0.2 s cadence, filtering self and inactive. Raycast cost at 10 tanks
   (~60/s wall ray each + LoS at 5 Hz) is trivial — confirmed non-issue.
   BotSpec asset: get-or-create (GUID stability, m1c 5b).

9. **LoS/avoidance rays: origin `muzzle.position`, and both rays MUST pass
   `QueryTriggerInteraction.Ignore` plus a mask excluding the projectile
   layer** (`~(1 << PotshotLayers.Projectile)`). Pickup trigger boxes are
   raycast-hittable by default — bots would see phantom LoS blocks and
   phantom walls at every pad — and in-flight shells otherwise flicker LoS
   during exactly the fights it matters. Muzzle is outside the firer's own
   collider at shell height y≈0.45 (inside the enemy 0–0.6 collider band), so
   ray height matches what a shot can actually do; aim the ray at the enemy
   hull center lifted to the same height, not barrel-forward (turret is
   cosmetic, m1c/m1d).

10. **Spawn selection edges.** Living = registered ∧ non-null ∧
    `activeInHierarchy` ∧ `!IsDead`. Empty living set (round reset, everyone
    dead at once) → fall back to round-robin/random, never Max over an empty
    sequence. Simultaneous respawns must not share a point: assign
    sequentially and count just-respawned tanks as living (or mark the point
    used this pass).

11. **SceneBuilder details.** Cover blocks "near center" vs the player tank
    at (0, 0.1, 0): place all four tanks at spawn-ring points and keep cover
    off them, or first activation overlaps a block (physics pop). Bot tanks:
    strip PlayerTankInput + PlaytestHotkeys + DevHud (existing dummy strip
    list) then AddComponent<BotBrain> — established prefab-override pattern.

12. **Standings HUD: put the OnGUI on the FfaGameMode object, not DevHud.**
    DevHud deactivates with the dead player — the scoreboard vanishes exactly
    during death and intermission, when it's wanted. Whatever hosts it must
    lazy-resolve and null-guard the game mode every OnGUI: WeaponTests/
    PickupTests instantiate the tank prefab (DevHud included) with no game
    mode in the scene.

13. **Minor.** (a) Bot-combat capture goes in Potshot.Tests.Visual via the
    gfx wrapper (m1b routing) — the task's probe line is consistent; keep it
    out of the merge gate. (b) The player tank registers like any bot —
    CameraFollow simply holds position over the corpse for 2 s; acceptable,
    note as intended. (c) Wall-grind test: assert displacement over a window,
    not instantaneous speed — bots legitimately stop to fire.
