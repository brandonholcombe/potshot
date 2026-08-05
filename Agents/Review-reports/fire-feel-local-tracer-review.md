# Review — fire-feel-local-tracer.md

## Reviewer: sonnet-reviewer
Task: `Agents/TODO/Active/fire-feel-local-tracer.md` (Author: fable-primary)
FishNet citations from the installed 4.7.2 source at
`game/Library/PackageCache/com.firstgeargames.fishnet@0728292d8339` (paths relative to it).

## Verdict: approve-with-changes

**Lead verdicts.** (1) The owner-side replicate guard `state.ContainsCreated() && !state.IsReplayed()`
is CORRECT — the owner fires the tracer exactly once per real input tick, replays are excluded, and no
owner-side "future" state exists. (2) The cooldown mirror must be a SEPARATE cooldown in NetworkWeapon
that reuses `WeaponController.Fire()` under a temporarily-swapped SpawnOverride — never `Tick()`
(`Tick` would spawn a full-damage offline projectile through the null SpawnOverride and corrupt the
mirrored ammo HUD via `ConsumeAmmo`). Exact mechanism in finding 2.

## Findings

1. **Owner replicate-state semantics verified — guard is exact.** Fresh owner tick:
   `Replicate_Authoritative` always invokes with `ReplicateState.Ticked | ReplicateState.Created`
   (`Runtime/Object/NetworkBehaviour/NetworkBehaviour.Prediction.cs:591`, "Owner always replicates with
   new data"). Reconcile replays: `Replicate_Replay_Authoritative` invokes only on an Exact history hit,
   always with `Replayed | Ticked | Created` (:759-775), and `Replicate_Current` early-outs entirely
   while reconciling (:504-505). The non-authoritative "future" state (`Replayed` alone, ReplicateState.cs:79)
   arises only in `Replicate_Replay_NonAuthoritative` (:781-826) — never on the controller. So
   `IsOwner && !IsServerStarted && state.ContainsCreated() && !state.IsReplayed()` fires once per real
   tick, zero on replays, and mirrors the existing server guard (NetworkTank.cs:140-142). `IsReplayed()`
   is `[Obsolete]` in 4.7.2 (ReplicateState.cs:58-59) — prefer `ContainsReplayed()` in the new line.

2. **Owner-path mechanism (prescribed).** In `NetworkWeapon` add a private `float _visualCooldown` and:
   `OwnerVisualTick(in TankInputSample sample, float dt)`: decrement `_visualCooldown`; return unless
   `sample.Fire && _visualCooldown <= 0f && _weapon.current != null`; gate on mirrored ammo
   (`if (_weapon.current.ammo > 0 && _weapon.AmmoLeft <= 0) return;`); set
   `_visualCooldown = _weapon.current.fireCooldown`; then swap-fire:
   `var prev = _weapon.SpawnOverride; _weapon.SpawnOverride = SpawnVisualTracer;
   _weapon.Fire(sample.AimWorldPos); _weapon.SpawnOverride = prev;`.
   `SpawnVisualTracer` calls `Projectile.Spawn(offlinePrefab, pos, velocity, VisualSpec(spec), firer)`
   with a cached `Object.Instantiate(spec)` clone (keyed by spec) whose `damage = 0; aoeRadius = 0` —
   never mutate the shared ScriptableObject. This reuses Fire's spread/mortar math (mortar arc for free,
   WeaponController.cs:112-124) while bypassing `Tick`'s cooldown/ammo mutation (WeaponController.cs:53-68).
   Also clamp `_visualCooldown = Mathf.Min(_visualCooldown, spec.fireCooldown)` in `MirrorToController`
   when the weapon id changes, mirroring Equip's anti-insta-fire clamp (WeaponController.cs:41-42).
   Damage-0 tracer hits are inert: client `TakeDamage(0,…)` only re-raises HealthChanged with an
   unchanged value (Damageable.cs:33-46); NetworkHealth subscribes server-side only.

3. **Spawning with the owner connection is safe.** `ServerManager.Spawn(go, ownerConnection)` exists
   (`Runtime/Managing/Server/ServerManager.QOL.cs:118`); bots pass null (unchanged). With
   `_clientAuthoritative = false` (set by PrefabFactory.cs:152) the owner gains NO transform authority:
   control checks return server-side authority (NetworkTransform.cs:1035-1036, 1100), and `_sendToOwner`
   defaults true (:420) so the owner still receives movement for its (hidden) shell. ProjectileNet's
   NetworkObject has prediction disabled, so ownership activates no prediction path. One real side
   effect: owned objects are despawned when the owner disconnects
   (`Runtime/Managing/Server/Object/ServerObjects.cs:211-243`) — for a ≤5 s shell this is negligible;
   accept (or set `PreventDespawnOnDisconnect` if it ever matters).

4. **IsOwner is valid in OnStartClient; renderer-off is sufficient.** The spawn message carries ownerId
   (`Runtime/Managing/Client/Object/ClientObjects.cs:453`) and `NetworkObject.InitializeEarly` calls
   `SetOwner(owner)` before behaviour init/callbacks (`Runtime/Object/NetworkObject/NetworkObject.cs:627-669`).
   Add the owner branch to the existing client strip in NetworkProjectile.OnStartClient (the host early-
   return at NetworkProjectile.cs:19 correctly keeps host shells visible — host never sees tracers).
   The NT keeps moving the invisible shell — harmless, ≤5 s. No trails/particles exist on the prefab.

5. **Muzzle origins verified both sides.** Muzzle sits under Turret under Graphical (PrefabFactory.cs:302-326).
   Owner: the smoother caches graphical world props in OnPreTick and restores them in OnPostTick
   (`Runtime/Generated/Component/TickSmoothing/TransformTickSmoother.cs:378-391, 433-457`) — it never
   snaps graphical to root during the tick, so `muzzle.position` read inside the replicate IS the
   on-screen barrel. Server: smoothers initialize client-only (`NetworkObject.Prediction.cs:314`), so the
   dedicated server's Graphical never moves — server fire origin unchanged, no regression. The known
   host-mode ~1-tick muzzle lag (net-visual-smoothing review, finding 3) is pre-existing and untouched.

6. **Pickups/layers: zero interference, confirmed.** WeaponPickup is a trigger; the tracer entering it
   fires only `OnTriggerEnter`, which requires a `TankController` parent and finds none (WeaponPickup.cs:37-46);
   Projectile has no trigger callback and triggers never produce OnCollisionEnter. Tracer-vs-shell and
   tracer-vs-tracer are killed by the Projectile-layer self-ignore (PotshotLayers.cs:14-16, plus the
   Configure belt-and-braces). Firer immunity covers the tracer via the existing collider-pair ignore.

7. **Freeze/death suppression suffices; one accepted ghost window.** PostMatch/frozen zeroes
   `sample.Fire` at the source (NetworkTank.BuildMoveData:104-112) and the tracer consumes the same
   replicate payload, so it freezes with the server path. Death despawns the tank (m2c prescription) —
   replicates stop. Accepted cosmetic: for ~RTT/2 after a death the owner may emit 1–2 tracers before
   the despawn arrives; same acceptance class as the task's drift note (#3).

8. **Tests — judged, with one restructure.** (a) Host-mode PlayMode genuinely cannot reach
   `IsOwner && !IsServerStarted`; do not fake it. (b) Spawn-ownership IS assertable in host mode: fire,
   find the ProjectileNet NetworkObject, assert `Owner.ClientId` equals the host client's id. (c) For
   the unit seams, do NOT test NetworkBehaviour instances offline (NetworkObject-bearing prefabs
   deactivate without a network — M2b). Instead keep the cooldown+swap-fire logic free of networked
   state so it is exercisable through a plain WeaponController on a bare GameObject, and extract the
   client strip into `NetworkProjectile.ApplyClientVisualPolicy(GameObject go, bool isOwner)` called
   from OnStartClient and directly from an EditMode/PlayMode test. Assert tracer harmlessness via a
   victim Damageable whose health is unchanged after impact.

9. **Deployment: bump GameVersion and ship server+client together.** Mixed versions are not dangerous
   but are ugly (new client on old server: ownerless shells → no hiding → firer sees tracer AND late
   shell). The VersionAuthenticator handshake already rejects mismatches — bumping the version makes the
   rollout atomic. Server first, then client build, is otherwise fine.

## Required changes (summary)

- Owner path: separate `_visualCooldown` + SpawnOverride swap around `Fire()` (finding 2); never `Tick()`.
- Use `ContainsReplayed()` (non-obsolete) in the new guard.
- Cached damage-0/aoe-0 spec CLONE for tracers; clamp `_visualCooldown` on mirrored weapon change.
- Testable seams: network-free tracer logic + extracted `ApplyClientVisualPolicy` (finding 8).
- GameVersion bump for atomic rollout (finding 9).
