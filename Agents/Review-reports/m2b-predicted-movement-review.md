# Review — m2b-predicted-movement.md

## Reviewer: sonnet-reviewer
Task: `Agents/TODO/Active/m2b-predicted-movement.md` (Author: fable-primary)
All FishNet citations are from the installed 4.7.2 source at
`game/Library/PackageCache/com.firstgeargames.fishnet@0728292d8339` (paths below relative to it).

## Verdict: approve-with-changes

## Findings

1. **API surface, verified — attributes are `[Replicate]`/`[Reconcile]` (NOT `[ReplicateV2]`), signatures enforced by codegen.**
   `Runtime/Object/Prediction/Attributes.cs` defines `ReplicateAttribute`/`ReconcileAttribute`.
   `CodeGenerating/Processing/Prediction/PredictionProcessor.cs:620-669` hard-errors unless:
   replicate = exactly `(TData data, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)`;
   reconcile = exactly `(TData data, Channel channel = Channel.Unreliable)`. Data types implement
   `IReplicateData`/`IReconcileData` (struct with `GetTick/SetTick/Dispose` + a private `uint _tick`).
   `TankInputSample` cannot be used directly; the planned `TankMoveData` wrapper is correct (public fields only —
   codegen builds a `PublicPropertyComparer<T>` for default-detection, `NetworkBehaviour.Prediction.cs:523`).
   Reconcile is built by overriding `public virtual void CreateReconcile()` (`NetworkBehaviour.Prediction.cs:1242`)
   and calling the `[Reconcile]` method from it, on BOTH server and client (client copy is the local fallback).

2. **Tick loop, verified — demo pattern is `TickNetworkBehaviour`, not raw PredictionManager wiring.** The canonical
   4.7.2 example `Demos/Prediction/Rigidbody/Scripts/RigidbodyPrediction.cs` extends
   `FishNet.Utility.Template.TickNetworkBehaviour` (`Runtime/Utility/Template/TickNetworkBehaviour.cs`), calls
   `SetTickCallbacks(TickCallback.Tick | TickCallback.PostTick)` in `OnStartNetwork()`, calls the replicate every
   `TimeManager_OnTick()` (passing `default` when not the controller — `BuildMoveData()` returns default unless
   `IsOwner`), and calls `CreateReconcile()` in `TimeManager_OnPostTick()`. TimeManager order per
   `Runtime/Managing/Timing/TimeManager.cs:739-755`: reconcile → OnTick (replicates) → `SimulatePhysics(TickDelta)`
   → OnPostTick → send states. Use this template; do not hand-subscribe TimeManager events.

3. **BLOCKING — physics mode: rigidbody prediction requires `PhysicsMode.TimeManager`, which forces
   `Time.fixedDeltaTime = 1/TickRate`; the plan's "30 Hz tick + 1/60 physics" is impossible.**
   Enum `FishNet.Managing.Timing.PhysicsMode { Unity=0, TimeManager=1, Disabled=2 }`
   (`Runtime/Managing/Timing/PhysicsMode.cs`). Reconcile replays only step physics when
   `tm.PhysicsMode == PhysicsMode.TimeManager` (`Runtime/Managing/Prediction/PredictionManager.cs:662,744`) —
   Unity mode silently breaks rigidbody replays. TimeManager mode sets `Physics.simulationMode = Script` and
   `Time.fixedDeltaTime = (float)TickDelta` (`TimeManager.cs:515-553`). Decision: keep TickRate 30 (serialized
   `_tickRate`, default 30, `TimeManager.cs:184`) and accept 33.3 ms physics while networked; `TankMotor.Step` is
   dt-parameterized so equilibrium speed is unchanged. Offline stays 1/60 (hub only instantiated on demand —
   `NetBootstrap.EnsureHub`). Required: NetworkFactory must `AddComponent<TimeManager>()` on the hub prefab and set
   serialized `_physicsMode = 1` (TimeManager) — the runtime auto-added component (`NetworkManager.cs:325`)
   defaults to Unity mode and prediction would silently misbehave. Document all of this in netcode.md.

4. **BLOCKING — test-suite pollution: destroying a hub does NOT restore physics settings.**
   `UnsetSimulationSettings()` (`TimeManager.cs:497-503`) only runs on application quit
   (`TimeManager.cs:320-331`). After any test starts a hub whose TimeManager uses PhysicsMode.TimeManager,
   `Physics.simulationMode` stays `Script` and `fixedDeltaTime` stays 1/30 for the rest of the PlayMode run —
   every subsequent M1 physics test dies (nothing simulates). Required: net-test teardown (including existing
   `NetHandshakeTests`) must call `TimeManager.SetPhysicsMode(PhysicsMode.Unity)` (public, `TimeManager.cs:589`)
   and restore `Time.fixedDeltaTime = 1/60f` after destroying the hub.

5. **BLOCKING — do NOT add NetworkObject to Tank.prefab; make a TankNet prefab variant.**
   `NetworkObject.TryStartDeactivation` (`Runtime/Object/NetworkObject/NetworkObject.cs:584-599`) calls
   `gameObject.SetActive(false)` from `Start()` whenever `_isNetworked` (default true) and no server/client is
   started — every offline tank (scene player, FfaGameMode bots, all M1 PlayMode tests) would deactivate itself.
   Potshot.Core cannot call `SetIsNetworked(false)` (no FishNet reference, by design). Resolution: PrefabFactory
   emits a second prefab (Tank + NetworkObject + NetworkTank, strip `PlaytestHotkeys`/`DevHud` or gate them by
   ownership) used only by NetGameMode; Tank.prefab and the M1 suite stay byte-identical. FishNet's
   `Generator : AssetPostprocessor` (`Runtime/Editor/PrefabCollectionGenerator/Generator.cs:18`) auto-adds the new
   prefab to DefaultPrefabObjects on save — commit the regenerated asset.

6. **NetworkObject serialized fields for the factory (exact names, `NetworkObject.Prediction.cs:145-252`):**
   `_enablePrediction` (bool, true), `_predictionType` (byte enum, `Rigidbody = 1`, internal — set via
   SerializedObject int), `_graphicalObject` (Transform), `_detachGraphicalObject`, `_enableStateForwarding`
   (default true — keep; it is what forwards inputs incl. turret aim to spectators), `_ownerInterpolation` (1),
   `_spectatorInterpolation` (2), `_adaptiveInterpolation`, `_enableTeleport`, `_teleportThreshold`. Recommended:
   reparent HullVisual + Turret under one `Graphical` child in the net variant and assign `_graphicalObject`,
   otherwise owners render raw 30 Hz steps and reconcile snaps unsmoothed (null is tolerated —
   `NetworkObject.Prediction.cs:380-383` — but netcode.md promises ~100 ms smoothing).

7. **Reconcile payload: manual state struct is right; PredictionRigidbody is optional here.** The demo funnels
   forces through `PredictionRigidbody` (`Runtime/Object/Prediction/PredictionRigidbody.cs`) because it preserves
   queued forces across reconciles; TankMotor is velocity-driven with no queued forces, so direct rb writes inside
   `[Replicate]` are fine. Use FishNet's `RigidbodyState` (`Runtime/Generated/Component/Prediction/RigidbodyState.cs`
   — pos/rot/isKinematic/linear+angular velocity, serializers included) via `rb.GetState()` in `CreateReconcile`
   and `rb.SetState(state)` in `[Reconcile]`, plus `BoostState.activeLeft/cooldownLeft` floats — exactly the M1g
   contract. Note `SetState` writes `transform.localPosition` — fine, tanks are scene roots.

8. **Bots through the same replicate path: verified.** `IsController => IsOwner || (IsServerInitialized &&
   !Owner.IsValid)` (`Runtime/Object/NetworkObject/NetworkObject.QOL.cs:171`); `Replicate_Authoritative` runs for
   `ownerlessAndServer` (`NetworkBehaviour.Prediction.cs:519`), and the demo comment says explicitly "this could be
   the server if no owner, for example AI". So server-driven bots = ownerless NetworkTank whose input is built from
   BotBrain's `ITankInput` — same `[Replicate]`. But the task's "bots remain scene-local … replicated to clients"
   is self-contradictory for this slice: scene-placed NetworkObjects need editor-baked SceneIds
   (`NetworkObject.Serialized.cs:100-204`; missing id throws, line 186) and deactivate offline (finding 5). Either
   runtime-spawn ownerless bot TankNets (small add) or explicitly defer bot replication to M2c; do not use scene
   NetworkObjects. The task's disable-scene-tank + runtime-spawn choice for players is confirmed correct.

9. **Empty-buffer semantics, verified:** when the server has no owner input queued it runs `default(TData)` with
   `state = ReplicateState.Ticked` (no `Created`) — `ReplicateDefaultData()`,
   `NetworkBehaviour.Prediction.cs:628-712`. Default input = zero Move → tank decelerates; acceptable. Guard the
   cosmetic turret: ignore `AimWorldPos` unless `state.ContainsCreated()` or it will snap toward world origin on
   packet loss (future-input handling reference: `Demos/Prediction/CharacterController/…Prediction.cs:270-330`).
   Bandwidth for aim-in-replicate: ~12 bytes @30 Hz, delta-serialized, well inside the netcode.md budget — fine.

10. **Spawning: signature verified —** `ServerManager.Spawn(GameObject go, NetworkConnection ownerConnection = null,
    Scene scene = default)` / `(NetworkObject nob, …)` (`Runtime/Managing/Server/ServerManager.QOL.cs:118,135`).
    Prefer FishNet's own pattern (`Runtime/Generated/Component/Spawning/PlayerSpawner.cs:86-113`): subscribe
    `SceneManager.OnClientLoadedStartScenes` (fires post-auth even with no global scenes,
    `Runtime/Managing/Scened/SceneManager.cs:496-502`), instantiate, `Spawn(nob, conn)`, then
    `SceneManager.AddOwnerToDefaultScene(nob)` — not raw `ServerManager.OnAuthenticationResult`.

11. **Mode split + tests: sound with adjustments.** Cleanest split: NetworkTank (on the net variant only) disables
    `TankController` in `OnStartNetwork` and owns turret rotation itself; PlayerTankInput's OnEnable claim is
    harmless (NetworkTank reads `controller.InputSource` to build owner data; disable PlayerTankInput on
    non-owners in `OnStartClient`). Offline regression is then structural (finding 5), which is stronger than a
    flag. In-process host test has real value: it exercises codegen, replicate flow, spawn, and physics-mode wiring
    (which finding 3/4 shows is the actual failure surface); assert position progression over ticks, and measure
    speed from position deltas per simulated tick (tick delta is 1/30 there, not 1/60). Multi-process authority
    stays M2d — agreed.

## Required changes
(3) explicit TimeManager component on hub with `_physicsMode = TimeManager` + netcode.md 1/30-physics note;
(4) physics-mode + fixedDeltaTime restoration in all net-test teardowns incl. NetHandshakeTests;
(5) separate TankNet prefab variant — never add NetworkObject to Tank.prefab;
(8) resolve the bot wording: runtime-spawned ownerless TankNet or defer to M2c — no scene NetworkObjects;
(9) turret aim guarded by `state.ContainsCreated()`;
(10) spawn via `OnClientLoadedStartScenes` + `AddOwnerToDefaultScene`.
