# Review — m1c-tank-movement.md

## Reviewer: sonnet-reviewer
Task: `Agents/TODO/Active/m1c-tank-movement.md` (Author: fable-primary)

## Verdict: approve-with-changes

Right slice, right conventions (Resources prefab, saved materials, generated
scene, ScriptedTankInput). But as written the movement code will be rewritten
in M2, and two test-plan details are structurally wrong. Fix findings 1–4.

## Findings

1. **BLOCKER — structure simulation for the M2 CSP conversion now.** FishNet
   prediction v2 replays a tick-stamped input struct through a `[Replicate]`
   method and restores state via `[Reconcile]`; a MonoBehaviour that polls
   `ITankInput` properties inside FixedUpdate cannot be replayed. Require:
   (a) `ITankInput` produces a plain struct `TankInputSample { Vector2 Move;
   Vector3 AimWorldPos; bool Fire; }` (becomes the `IReplicateData` payload in
   M2 — only tick fields get added); (b) the motor exposes one method,
   e.g. `TankMotor.Step(in TankInputSample input, float dt)`, that contains
   ALL movement math, reads no `Time.*`/`Input.*`/`ITankInput` internally, and
   is dt-parameterized — M1 calls it from FixedUpdate with fixedDeltaTime, M2
   calls it from Replicate with the tick delta (netcode.md is 30 Hz vs physics
   60 Hz — hardcoded 1/60 assumptions mean retuning later); (c) hull rotation
   via `rb.MoveRotation`/rotation property, never `transform` writes, so
   reconcile state capture (`PredictionRigidbody`) sees it. Turret aiming is
   cosmetic — keep it OUT of `Step` (own component, plain Update is fine); it
   never gets predicted, only the aim value ships to the server.

2. **BLOCKER — pin the accel model and its math; "exponential approach" fails
   the tests as specified.** A `Lerp(v, target, k*dt)` style model never
   reaches 6 u/s, so "(a) top speed within 5%" is boundary-exact with "(b)
   0→95% in 0.3–0.5 s" — the same 95% threshold, satisfied only marginally
   and tolerance-fragile. Specify linear `MoveTowards(v, move*topSpeed,
   accel*dt)`: with accel 15 u/s² → 95% at exactly 0.38 s and top speed hit
   *exactly* (clamped); decel 15–20 u/s² → full stop in 0.3–0.4 s (test c
   passes); turret 360°/s → 90° in 0.25 s (test d passes). Put accel/decel in
   TankSpec. Tests must measure time by counting `WaitForFixedUpdate` yields ×
   `Time.fixedDeltaTime` (SmokeTests pattern), never `Time.time` deltas.

3. **BLOCKER — the PlayMode merge-gate tests cannot use DevArena.**
   `Potshot.Tests.PlayMode.asmdef` has `includePlatforms: []` — no UnityEditor
   APIs, so no `LoadSceneInPlayMode`; and DevArena is (correctly) not in build
   settings. The wall-collision test must build its floor + one wall from
   primitives in test code (m1b finding 6 corollary). State this in the task,
   including that the tank is spawned via `Resources.Load` + a
   `ScriptedTankInput` injected through a public setter/field on the motor —
   which also forces the task to say how the input source is selected
   (recommend: spawn-time injection; the prefab carries no PlayerTankInput,
   DevArena bootstrap adds it).

4. **Rigidbody settings — specify them.** (a) `constraints = FreezeRotationX |
   FreezeRotationZ` — a velocity-driven box hitting a wall edge will tip and
   roll otherwise; (b) `interpolation = Interpolate` for smooth camera follow
   (note in code: FishNet wants this off on predicted objects in M2, its
   smoothing takes over); (c) collision detection Discrete is fine — 6 u/s is
   0.1 u per fixed step, no tunneling vs 1 u walls (shells at 14 u/s are the
   later slice's problem); (d) drag = 0, decel handled manually in `Step` —
   never mix solver drag with the tuned decel; (e) when assigning
   `rb.linearVelocity`, write only XZ and preserve the y component so gravity
   still settles the tank. Velocity assignment (not MovePosition) is correct
   against walls — the solver zeroes the into-wall component; keep it.

5. **Prefab factory details.** `SaveAsPrefabAsset` overwrite preserves the
   GUID — good. Add: (a) `PrimitiveType.Cylinder` ships a CapsuleCollider —
   strip all child colliders; exactly one BoxCollider on the hull root, or the
   compound collider snags walls with the barrel; (b) TankSpec asset must be
   get-or-create (Mat() pattern) — delete/recreate changes the GUID and
   silently breaks the prefab's serialized reference on re-run; (c)
   `Directory.CreateDirectory("Assets/Resources/Prefabs")` first. TankSpec
   itself needn't live in Resources — the prefab reference pulls it into any
   build; EditMode tests load it via AssetDatabase. Resources at this scale
   (one prefab, few materials): no build-size concern; the server build needs
   the tank anyway. TankSpec class goes in Potshot.Core, factory in EditorTools.

6. **Visual test: after `EnterPlayMode`, only `yield return null` advances.**
   The test is still an EditMode-runner coroutine; `WaitForFixedUpdate` /
   `WaitForSeconds` are unsupported there. Drive "1 s of scripted driving" as
   `while (Time.time < t0 + 1f) yield return null;` — frames advance play mode
   and fixed steps run inside them (count is approximate; fine for a visual).
   Exact fixed-step assertions belong only in the PlayMode gate (finding 2).

7. **Minor.** (a) Camera-follow numeric asserts would be overreach — the PNG
   read covers framing; at most assert the camera moved with the tank. (b)
   Hull yaw rate ("hull yaws toward move direction") has no constant — add
   `hullTurnDegPerSec` to TankSpec so it's tunable, even though no test pins
   it. (c) `Fire` is dead until the weapons slice — fine, keep the interface
   complete. Scope is otherwise right; nothing needs to move out.
