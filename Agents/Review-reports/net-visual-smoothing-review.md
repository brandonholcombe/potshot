# Review — net-visual-smoothing.md

## Reviewer: sonnet-reviewer
Task: `Agents/TODO/Active/net-visual-smoothing.md` (Author: fable-primary)
FishNet citations from the installed 4.7.2 source at
`game/Library/PackageCache/com.firstgeargames.fishnet@0728292d8339` (paths relative to it).

## Verdict: approve-with-changes

**THE component for 4.7.2 predicted rigidbodies: `NetworkObject._graphicalObject` (the built-in
`TransformTickSmoother` "PredictionSmoother" path) — not a new component.** Wiring: add an
identity-transform `Graphical` child to the tank root, move `HullVisual` + `Turret` (with `Barrel`/
`Muzzle`) under it, and in `CreateTankNetPrefab` set on the NetworkObject via SerializedObject:
`_graphicalObject` → the Graphical transform, keep defaults `_ownerInterpolation=1`,
`_spectatorInterpolation=2`, `_adaptiveInterpolation=Low`, `_detachGraphicalObject=false`; set
`_enableTeleport=true`, `_teleportThreshold≈5`. Colliders + Rigidbody stay on the root.

## Findings

1. **Smoother mechanism verified.** Two coexist in 4.7.2. (a) Legacy: `NetworkObject._graphicalObject`
   (`Runtime/Object/NetworkObject/NetworkObject.Prediction.cs:170`, fields 211-252) drives a
   `TransformTickSmoother` (`Runtime/Generated/Component/TickSmoothing/TransformTickSmoother.cs`),
   initialized client-only when `_enablePrediction` (`InitializeSmoothers`, NetworkObject.Prediction.cs:371-392,
   called `!asServer` at :314). It is hooked to ALL prediction events unconditionally — `OnPreTick`:443,
   `OnPostReplicateReplay`:452, `OnPostTick`:461, `OnPreReconcile`:470, per-frame `OnUpdate`:284-291 —
   and handles owner (interp 1) vs spectator (interp 2 / adaptive-by-RTT, TransformTickSmoother.cs:281-342)
   automatically. (b) Beta: `NetworkTickSmoother` (`.../TickSmoothing/NetworkTickSmoother.cs:11`,
   namespace `FishNet.Component.Transforming.Beta`) placed ON the graphical child with
   `InitializationSettings.TargetTransform` = root (graphical==target is rejected,
   `UniversalTickSmoother.cs:396-399`). REJECT (b) for this task: its replay/reconcile correction is only
   subscribed when adaptive interpolation is on (`TickSmootherController.cs:258-272`), it adds a
   NetworkBehaviour to the prefab, and it needs nested-struct serialization from the factory. The legacy
   field is only obsolete-annotated for v5 removal (`NetworkObject.Prediction.cs:129`); the serialized
   `_graphicalObject` field itself carries no `[Obsolete]`, and setting it via SerializedObject compiles
   warning-free. Graphical need not be a DIRECT child — goals are computed as root world props + the
   init-time offset (`GetNetworkObjectWorldPropertiesWithOffset()`, TransformTickSmoother.cs:551,
   offsets captured at :248) — but direct child is the sane structure. Direct child, identity local
   transform (m1d scaled-parent lesson: NEVER scale the Graphical node itself).

2. **Diagnosis mechanically confirmed.** `PhysicsMode.TimeManager` sets
   `Physics.simulationMode = SimulationMode.Script` (`Runtime/Managing/Timing/TimeManager.cs:520`) and
   steps `Physics.Simulate(tickDelta)` inside the tick loop after `OnTick` (TimeManager.cs:744-748,
   1086-1092). Unity rigidbody interpolation only functions under automatic FixedUpdate simulation —
   under Script mode the `RigidbodyInterpolation.Interpolate` set in `CreateTankPrefab`
   (PrefabFactory.cs:240) is inert, and no FishNet code compensates absent a smoother. TankNet today has
   NO `_graphicalObject` set (PrefabFactory.cs:75-92) → InitializeSmoothers takes the null branch
   (NetworkObject.Prediction.cs:380-384, silently). 33 ms teleports confirmed as the root cause.

3. **Restructure the SHARED Tank.prefab, not TankNet-only.** TankNet is rebuilt by unpacking Tank.prefab
   (PrefabFactory.cs:67-72), so restructuring the base gives TankNet the node for free; offline is
   unharmed (a grouping child is visually inert; the offline root still gets real FixedUpdate rigidbody
   interpolation since no hub → no physics-mode change, NetBootstrap.cs:34-49). No hierarchy-path
   dependencies exist outside PrefabFactory (grepped: no `transform.Find`/child-index use on tanks).
   Keep `controller.turret`, `weapon.muzzle` assignments pointing at the same (re-parented) transforms.
   Note one host-mode quirk to document: on a listen host the client-side smoother lags the graphical
   (and thus Muzzle) ~1 tick behind the root; server firing from `muzzle.position` on a host spawns
   shells fractionally behind the hull (~0.2 u at top speed). Dedicated server never smooths
   (InitializeSmoothers is client-only), so tow-c1 is unaffected. Accept.

4. **Turret cosmetic is safe — verified.** The smoother writes ONLY the `_graphicalObject` transform's
   world pos/rot/scale (`MoveToTarget`, TransformTickSmoother.cs; queue entries hold that single
   transform's properties). Children keep local state. `TankController.Update` (TankController.cs:60-69)
   assigns turret WORLD rotation toward `AimWorldPos` every frame, after FishNet's update (NetworkManager
   runs at `[DefaultExecutionOrder(short.MinValue)]`, `Runtime/Managing/NetworkManager.cs:39`), so aim
   overrides any parent-rotation drift the same frame. No stomp either direction. Task item 2 resolves to
   "no code change needed" beyond the factory keeping the turret reference.

5. **ProjectileNet needs NO change — camera is the real bullet-ghosting vector.** NetworkTransform
   already smooths at render rate on remote clients: `TimeManager_OnUpdate` → `MoveToTarget(deltaTime)`
   (`Runtime/Generated/Component/NetworkTransform/NetworkTransform.cs:780-786, 1616-1717`) MoveTowards-es
   the transform each frame using rates spanning tickDelta, buffered over `_interpolation` (ushort,
   default 2, :386 — factory leaves default; `_clientAuthoritative=false` + componentConfiguration
   Rigidbody are correct, PrefabFactory.cs:117-130; server-auth spectators pass `DoSettingsAllowSmoothing`
   :1723-1740). 2 ticks ≈ 66 ms latency at 30 Hz — fine for 14-30 u/s shells. The perceived bullet
   strobing is dominated by the camera hard-latching to the 30 Hz-stepped tank root: the whole frame
   jumps 33 ms while bullets glide, reading as ghosting on everything. Fix the camera, re-judge bullets.

6. **REQUIRED CHANGE — camera must target the Graphical child, not root-with-lowpass.**
   `NetworkTank.OnStartClient` sets `follow.target = transform` (NetworkTank.cs:70) and CameraFollow
   hard-latches in LateUpdate (CameraFollow.cs:11-15). Exponential smoothing of the STEPPED root at
   8-12 /s (plan item 4) only attenuates the 30 Hz sawtooth and adds lag; the correct fix is
   `follow.target = <Graphical transform>` (moves at render rate, updated before LateUpdate per finding 4's
   execution order). Then exponential smoothing becomes optional polish — fine to add (frame-rate
   independent form: `Lerp(pos, goal, 1 - Mathf.Exp(-k * Time.deltaTime))`), keep offline targeting root.

7. **Smoothness test: feasible — but use `yield return null`, NOT WaitForEndOfFrame.** WaitForEndOfFrame
   does not fire reliably in batchmode; it is also unnecessary: smoothing runs in the Update phase
   (findings 1, 5), so frame boundaries suffice. Prescription: NetMoveTests-pattern host fixture
   (Tests/PlayMode/NetMoveTests.cs:27-66 — reuse its physics-mode teardown), drive scripted forward
   input, wait until at speed, then for ~60 frames record `(root.position, graphical.position)` each
   `yield return null`. Assert (a) ≥1 frame pair where root is unchanged but graphical moved
   (intermediate render positions exist — the actual smoothness property), and (b) graphical z stays
   within [min,max] of neighboring root samples (no overshoot). Batchmode's uncapped frame rate gives
   many frames per 33 ms tick, making (a) robust. Keep Brandon's eyes as the final gate.

8. **TickRate 30→60: GO, but sequenced last and it is NOT client-only.** All game sim code is
   dt-parameterized (`TankMotor.Step`/`ServerTick` take `TimeManager.TickDelta`, NetworkTank.cs:124-131;
   specs are per-second units) and prediction buffers/interpolation are tick-denominated (they halve in
   real time — beneficial). Costs: ~2x replicate/reconcile/NT bandwidth and 2x server physics CPU
   (trivial at 10 players). `_tickRate` is serialized on the hub's TimeManager (TimeManager.cs:184,
   default 30; NetworkFactory.cs:34 leaves default) and is baked into BOTH builds — changing it demands
   a coordinated server redeploy + client rebuild (version-gated by VersionAuthenticator, so mismatched
   clients are already rejected). Do it only if 1-4 still feel steppy, as its own deploy.

9. **Deployment scope correction (plan item 7):** treat server redeploy as REQUIRED, not "for
   consistency". Spawn messages serialize per-NetworkBehaviour/prefab data; client and server must run
   identical prefab assets. The legacy-path change adds no NetworkBehaviour (good — component indexes
   stable) but changes serialized NetworkObject data and hierarchy. Rebuild both, deploy together.

## Summary of required changes
- Use `NetworkObject._graphicalObject` (+ defaults above), not a new smoother component (finding 1).
- Restructure base Tank.prefab; TankNet inherits; set the field only in CreateTankNetPrefab (finding 3).
- Camera: retarget to Graphical on client — smoothing alone is insufficient (finding 6).
- Test via `yield return null` frame sampling, not WaitForEndOfFrame (finding 7).
- Server redeploy mandatory; TickRate change (if ever) is a separate coordinated deploy (findings 8, 9).
