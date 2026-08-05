# Review — m2c-networked-combat.md

## Reviewer: sonnet-reviewer
Task: `Agents/TODO/Active/m2c-networked-combat.md` (Author: fable-primary)
All FishNet citations from the installed 4.7.2 source at
`game/Library/PackageCache/com.firstgeargames.fishnet@0728292d8339` (paths relative to it).

## Verdict: approve-with-changes

## Findings

1. **SyncType syntax, verified — `[SyncVar]` is dead; use readonly `SyncVar<T>` fields.**
   `Runtime/Object/NetworkBehaviour/Attributes.cs:174` marks `SyncVarAttribute`
   `[Obsolete("This no longer functions. Use SyncVar<Type> instead...")]`. The 4.7.2 pattern
   (in-package example `Runtime/Generated/Component/TakeOwnership/PredictedOwner.cs:39`):
   `private readonly SyncVar<float> _health = new();` inside a NetworkBehaviour; read/write via
   `.Value` (`Runtime/Object/Synchronizing/SyncVar.cs`, `Value` property → `SetValue`); change hook
   `_health.OnChange += (prev, next, bool asServer) => ...`. Codegen REQUIRES `readonly` (or
   `[AllowMutableSyncType]`) — `CodeGenerating/Processing/SyncTypeProcessor.cs:264-283` hard-errors
   otherwise; never reassign the field. Same shape for `SyncDictionary<int,int>` and `SyncList<T>`
   (`Runtime/Object/Synchronizing/SyncDictionary.cs` — `SyncBase, IDictionary<TKey,TValue>`).
   Server-only write / observer read are the defaults. For the round timer use
   `new SyncVar<float>(new SyncTypeSettings(1f))` — sendRate ctor at
   `Runtime/Object/Synchronizing/SyncTypeSetting.cs:22`; don't sync it every tick.

2. **Death/respawn prescription: Despawn + fresh Spawn. Deactivation is NOT an option.**
   FishNet has no active-state replication for spawned root objects: the only `SetActive` calls in the
   runtime are pooling/pre-spawn bookkeeping (`Runtime/Managing/Object/ManagedObjects.cs:315-336`,
   `Server/Object/ServerObjects.cs:523`), and `NetworkObject.OnDisable` (`NetworkObject.cs:406`) syncs
   nothing — a server-side `SetActive(false)` leaves the tank visible on every client. Worse, a
   deactivated tank keeps replicating: `TickNetworkBehaviour` subscribes plain C# TimeManager events in
   `OnStartNetwork_Internal` and only unsubscribes in `OnStopNetwork_Internal`
   (`Runtime/Utility/Template/TickNetworkBehaviour.cs:42-56`), so `TimeManager_OnTick` →
   `PerformReplicate` still runs on an inactive object. So: on death capture credit, queue the action,
   and next tick `ServerManager.Despawn(nob)`; after respawnDelay re-Instantiate TankNet at the picked
   spawn and `ServerManager.Spawn(nob, conn)` (bots: no conn). Camera re-target and prediction state
   rebuild for free via the existing `OnStartClient`/`OnStartNetwork`. `Despawn` default = Destroy:
   `ServerManager.QOL.cs:150,167` resolve `GetDefaultDespawnType()` (`NetworkObject.cs:315`, enum
   `NetworkObjectData.cs:6-10`), executed at `ManagedObjects.cs:296-299`. Pooling is a later
   optimization. Consequence: server scores CANNOT key on TankController instances — key the
   SyncDictionary by `Owner.ClientId`, with synthetic negative ids for bots. Keep the
   never-mutate-inside-Died rule (queue, act from the tick), matching FfaGameMode's precedent.

3. **Reuse boundary — NetFfaState is a parallel server implementation, not FfaGameMode reuse.**
   FfaGameMode is instance-keyed, subscribes `Died` per-tank, and respawns via
   `SetActive(true)` on the same object — all three conflict with finding 2. Realistic reuse: extract
   `PickSpawn` (farthest-from-living) into a static Core helper both modes call, and keep the scoring
   rule (+1 kill / −1 self-or-env) as a tiny pure function; everything else in NetFfaState
   (NetworkBehaviour, ~150 lines) is new code. Do not run FfaGameMode on the server. Also: NetFfaState
   CANNOT live on the hub — `NetworkManager.cs:312-313` logs "NetworkObject component found on the
   NetworkManager object... not allowed". Make it a factory-built prefab the server spawns on start.
   Since `CleanOfflineActors` disables FfaGameMode (its OnGUI too), NetFfaState must draw the client
   scoreboard itself from the SyncDictionary.

4. **NetworkWeapon reuse is sound; the seam is an instance event, not a Projectile rewrite.**
   `[Replicate]` passes the sample; only `IsServerStarted` calls `weaponController.Tick(sample,
   (float)TimeManager.TickDelta)`. To network the shells WeaponController needs one Core addition:
   `public event Action<Projectile> ProjectileSpawned;` invoked after each `Projectile.Spawn` in
   `Fire`/`FireMortar` (instance event — statics are banned by test-leakage precedent). NetworkWeapon
   subscribes server-side, swaps `projectilePrefab` to ProjectileNet, and calls
   `ServerManager.Spawn(p.gameObject)` per pellet. Cooldown math at TickDelta 1/30 still yields MG
   10/s (0.1s cooldown ≥ 3 ticks) — no rate skew.

5. **Non-NetworkBehaviour server check: the shim is mandatory, not just cleanest.** Potshot.Core's
   asmdef references nothing — `Projectile` cannot see FishNet, so `InstanceFinder` (exists:
   `Runtime/InstanceFinder.cs`, `IsServerStarted` line 161) is unusable there, and undesirable anyway:
   its getter caches the first manager and `Debug.Log`s "NetworkManager not found" on every offline
   access (`InstanceFinder.cs:27-55`) — log spam offline, and a statics seam against our per-test
   hubs. Use a `NetworkProjectile : NetworkBehaviour` shim on ProjectileNet (its own
   `IsServerInitialized`). Critical client-side duty: in `OnStartClient` when `!IsServerStarted`,
   **Destroy the Projectile component and disable the collider** — Unity delivers
   `OnCollisionEnter` to disabled MonoBehaviours, and a live client Projectile would apply local
   damage, deactivate tanks client-side, and `Destroy` a spawned NetworkObject after 5s lifetime.
   (Server-side `Destroy(gameObject)` inside Projectile is safe as-is: unexpected destroys broadcast a
   despawn via `ServerObjects.cs:1094-1098` `FinalizeDespawn(nob, DespawnType.Destroy)` — no
   Projectile despawn rewrite needed this slice.)

6. **NetworkTransform, verified — `FishNet.Component.Transforming.NetworkTransform`**
   (`Runtime/Generated/Component/NetworkTransform/NetworkTransform.cs:21-25`). Defaults:
   `_clientAuthoritative = true` (line 414) — MUST be set false via SerializedObject (no public
   setter exists); `_interval = 1` tick (line 444, `SetInterval` line 1098); `_interpolation = 2`
   ticks (line 386); position/rotation packed (lines 368-373). Set `_componentConfiguration =
   Rigidbody` (enum value 2, lines 29-35): with server-auth it makes the rigidbody kinematic on
   pure clients (`CanMakeKinematic`, lines 897-905) so client physics never fights the sync.
   Bandwidth: worst case ~40 live shells × ~15 B × 30 Hz ≈ 18 kB/s/client — fine for 10 friends;
   `SetInterval(2)` is the knob if it ever isn't. No prediction on ProjectileNet.

7. **Feel + judgment calls.** (a) No projectile prediction: accept — shells render ~RTT/2 + 66 ms
   (2-tick interpolation) after the click; at friends latency that is playable, and the task already
   earmarks an M5 local tracer. (b) **Pickups: defer** — the slice is full, and weapon/ammo HUD sync
   (below) is prerequisite work anyway. But if deferred, `CleanOfflineActors` MUST also disable
   `PickupPad`s, or every client runs phantom local pickups that equip client-side WeaponControllers.
   (c) Fixed 3 bots: fine. (d) `-potshotAutofire` E2E: genuinely scriptable (an ITankInput that
   claims `InputSource` before PlayerTankInput, gated by the arg) — keep as nice-to-have, don't gate
   the milestone on it. (e) Tests: per-test-port hubs are safe as long as project code never touches
   InstanceFinder statics (finding 5) — currently true; keep it that way.

8. **Client HUD/state sync the plan under-specifies.** Server-only firing means client
   WeaponController/Damageable never change: DevHud would show stale ammo/health forever.
   NetworkWeapon needs `SyncVar<string>` weapon id (or index) + `SyncVar<int>` ammo; NetworkHealth's
   `SyncVar<float>` needs a Core write-back (e.g. `Damageable.SetHealthFromNetwork(float)`) or DevHud
   must read the SyncVars directly. Intermission freeze: sync `RoundActive` as `SyncVar<bool>` and set
   `InputFrozen` from its OnChange on each side — the server zeroing inputs alone would rubber-band
   against client prediction.

## Required changes
(2) despawn+respawn model, ClientId-keyed scores, queued death actions; (3) NetFfaState as spawned
singleton prefab + own scoreboard, PickSpawn extracted, no FfaGameMode on server; (4)
`ProjectileSpawned` instance event seam; (5) NetworkProjectile shim destroys Projectile + collider on
clients; (6) `_clientAuthoritative=false` + `_componentConfiguration=Rigidbody` via SerializedObject
on ProjectileNet; (7b) disable PickupPads in networked sessions when deferring pickups; (8)
weapon/ammo/health SyncVars + RoundActive-driven freeze.
