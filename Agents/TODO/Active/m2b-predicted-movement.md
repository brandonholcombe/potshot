# M2b — Server-authoritative tank movement with client prediction

## Author: fable-primary
## Status: Not Started

The netcode core: tanks become NetworkBehaviours; the owner predicts with
the SAME TankMotor.Step the server runs; reconciliation restores pose,
velocity, and BoostState. Offline mode must keep working unchanged.

## Plan

1. **NetworkTank** (Potshot.Net, on the tank prefab next to
   TankController): FishNet prediction v2 —
   - `[Replicate] void Move(TankMoveData data, ...)` where TankMoveData
     wraps TankInputSample (Move/AimWorldPos flattened; Fire/Boost bools)
     — built FROM the owner's ITankInput each tick.
   - `[Reconcile] void Reconcile(TankReconcileData data, ...)` carrying
     position, rotation, linear+angular velocity, BoostState (the M1g
     contract), applied then replayed through TankMotor.Step.
   - Uses FishNet's PredictionManager tick loop (OnTick / OnPostTick per
     FishNet 4.x prediction v2 docs); RigidbodyPauser/NetworkCollision
     considerations per docs.
   - Turret stays cosmetic: owner-local instant, others get aim via the
     replicate data applied in Update lerp (no prediction).
2. **Mode split in TankController**: when a NetworkTank is active
   (networked session), TankController.FixedUpdate defers to it (flag or
   component-enabled switch); offline (no network) path unchanged — all
   M1 PlayMode tests must stay green as-is.
3. **Prefab**: PrefabFactory adds NetworkObject + NetworkTank to the tank
   prefab; NetworkObject settings for prediction (IsNetworked defaults;
   prediction settings per FishNet 4.x — reviewer to verify which flags
   are needed on NetworkObject for rigidbody prediction).
4. **Spawning**: NetGameMode stub (Potshot.Net): on client authenticated,
   server spawns a tank for that connection at a spawn point and gives
   ownership. DevArena/map scenes: existing scene-placed player tank is
   DISABLED in networked sessions (server spawns instead); bots remain
   scene-local server-side (server-driven, replicated to clients as
   non-owned tanks). Full FFA integration is M2c — this slice: spawn +
   drive.
5. **Physics scene note**: FishNet prediction with Rigidbody requires
   the tick-aligned physics FishNet manages (TimeManager physics mode) —
   set TimeManager to the recommended mode for prediction (verify:
   Physics.simulationMode manual vs FishNet's; ProjectConfigurator may
   need a change; fixedDeltaTime vs tick rate 30 Hz — tick delta 1/30
   while physics 1/60: FishNet docs recommendation wins; document it).
6. **Tests** (batchmode gate): in-process host — owned tank moves under
   scripted input through the networked path (position advances, speed
   matches spec); a second dummy connection... (single-process limit:
   drive what's testable — server-auth position progression + offline
   regression suite). Multi-process authority tests arrive in M2d.
7. Docs: netcode.md prediction section updated with the real implementation.

## Risks

- FishNet prediction v2 API surface is the least-familiar part —
  reviewer must verify the exact 4.7.2 method signatures
  ([ReplicateV2]/[ReconcileV2] naming changed across 4.x versions).
- 30 Hz tick vs 60 Hz physics interplay.
- Offline regression: the M1 suite is the guard.
