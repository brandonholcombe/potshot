# Net — Fix tick-stepping ghosting (tanks + bullets)

## Author: fable-primary
## Status: Complete (2026-08-04) — smoother wired (graphical child + None interpolation), inter-tick glide test green; deployed ad9dddb both sides

Brandon (2026-08-04): "constant ghosting with tanks and bullets, it's
disorienting." Diagnosis: networked physics steps at 30 Hz
(PhysicsMode.TimeManager) with no graphical smoothing anywhere —
rigidbody interpolation is inert under manual stepping, we never wired
FishNet's graphical-object smoothing, and the follow camera latches to
the stepped transform. Everything moves in 33 ms teleports.

## Plan (reviewer to verify each against 4.7.2 source)

1. **TankNet graphical smoothing**: restructure the tank prefab so ALL
   visuals (hull, turret, barrel) sit under one `Graphical` child;
   wire FishNet's tick smoother (verify: component name in 4.7.2 —
   NetworkTickSmoother / MonoTickSmoother? — and/or NetworkObject
   `_graphicalObject` field semantics for prediction: detach/reattach,
   ownerSmoothing settings). Colliders stay on the simulated root.
   Applies to BOTH prefab variants? (Offline Tank.prefab renders fine
   with normal FixedUpdate interpolation — verify whether restructure
   hurts offline; prefer restructuring only TankNet if the factory can
   cleanly re-parent, else shared structure with smoother disabled
   offline.)
2. **Turret cosmetic chain**: TankController.Update rotates the turret
   transform — after restructure the turret visual lives under
   Graphical; verify the aim still applies (turret reference must point
   at the graphical turret).
3. **ProjectileNet**: verify NetworkTransform interpolation settings
   (interpolation value, tick smoothing on the NT itself) — clients
   should glide between states; if NT interpolates the TRANSFORM
   directly, kinematic-body config is fine, but confirm no per-tick
   snap (NT _interpolation=2 default claimed in m2c review — confirm
   applied and sufficient at 30 Hz for 14-30 u/s shells).
4. **CameraFollow smoothing**: exponential smoothing toward target
   (t ≈ 8-12 /s) instead of hard latch; keep offline feel identical
   (small smoothing is an improvement there too).
5. **Optional, measure after 1-4**: TickRate 30→60 on the TimeManager
   (bandwidth roughly doubles, trivial at friend-scale; physics delta
   halves). Only if smoothing alone still feels steppy.
6. **Verification**: gate stays green; visual proof — headless-driven
   host with a moving tank captured at high framerate? (Screenshots
   can't show smoothness; instead capture N successive frames at
   render rate and assert intermediate positions BETWEEN tick
   positions exist — a real smoothness assert. Reviewer: judge
   feasibility; fallback is Brandon's eyes after deploy.)
7. Client-only change set (prefab + camera) — server rebuild only if
   prefab data changes affect the server build (same prefab asset —
   redeploy anyway for consistency).

## Risks

- FishNet prediction + graphical smoothing interplay has version-
  specific pitfalls (detached graphical objects, teleport thresholds) —
  source verification is the point of this review.
- Restructure touches PrefabFactory tank geometry paths (muzzle,
  turret refs — m1d's scaled-parent lesson applies).
