# Review — scene-load-tick-burst.md

## Reviewer: sonnet-reviewer
Task: `Agents/TODO/Active/scene-load-tick-burst.md` (author: fable-primary).
FishNet 4.7.2 source cited from `game/Library/PackageCache/com.firstgeargames.fishnet@0728292d8339`
(as `Runtime/...`).

## Verdict: wrong-diagnosis

**The tick-debt catch-up hypothesis is refuted by source.** FishNet clamps ticks-per-frame (default 3,
tick dropping ON — confirmed in our prefab) and resyncs the client tick by a JUMP, never by simulating
the missed window. A scene-load stall can produce at most ~100 ms of extra sim in one frame. The real
bug: **the match-start global load of DevArena resurrects the scene's OFFLINE actors** (keyboard-driven
Player tank, 3 BotBrain bots, enabled FfaGameMode, PickupPads) on both client and server —
`NetBootstrap.CleanOfflineActors` runs only once at connection start, never after FishNet scene loads.
On the client those unpaused offline rigidbodies are additionally multi-stepped by every prediction
reconcile replay, so a ghost tank mirroring the player's exact keyboard input drives around at several
times spec speed, and collides with the predicted tank via contacts the server does not have.

## Findings

1. **Tick accumulation is clamped — no burst.** `IncreaseTick()` accumulates `Time.unscaledDeltaTime`
   into `_elapsedTickTime` and runs whole ticks in a do/while (`Runtime/Managing/Timing/TimeManager.cs:688-778`).
   With `_allowTickDropping` (default `true`, :171) the excess is discarded:
   `if (ticksCount > _maximumFrameTicks) _elapsedTickTime = timePerSimulation * _maximumFrameTicks`
   (:710-715; `_maximumFrameTicks` default 3, :178). Our generated hub carries the defaults —
   `game/Assets/Resources/Prefabs/NetworkHub.prefab` lines 76-78: `_allowTickDropping: 1`,
   `_maximumFrameTicks: 3`, `_tickRate: 30`. Post-stall worst case: 3 ticks (≈100 ms) in one frame, once.
2. **Client resync is a jump, not a replay.** `ParseTimingUpdate` recomputes
   `Tick = (LocalTick - clientTick)/2 + LastPacketTick.RemoteTick + 1` and assigns it directly
   (TimeManager.cs:1204-1211). If the correction exceeds 4 ticks it RESETS `_adjustedTickDelta = TickDelta`
   (:1213-1219); otherwise it nudges the client tick delta by 1 % (`ChangeAdjustedTickDelta`, :1130-1137).
   Dropped ticks are simply lost — FishNet's design already absorbs stalls. The task's Risk note was right:
   no timing hack is needed, and none should be added.
3. **Owner replicate replays cannot add net speed.** Each reconcile restores the rigidbody
   (`NetworkTank.PerformReconcile` → `_rb.SetState`, `game/Assets/Scripts/Net/NetworkTank.cs:165-175`)
   before the replay loop re-simulates to present (`Runtime/Managing/Prediction/PredictionManager.cs:693-721`).
   Rollback + replay is displacement-neutral for predicted objects.
4. **Double-step candidate: refuted, zero-frame window.** Client spawns activate the object and call
   `NetworkObject.Initialize(false,false)` (→ `OnStartNetwork`, which sets `ExternallyDriven = true`,
   NetworkTank.cs:49-56) in the same C# call stack: `ObjectCaching.Iterate()` instantiates (:301),
   `SetActive(true)` (:377-378), then initializes all spawns (:419-431). Unity cannot interleave a
   FixedUpdate mid-stack, so `TankController.FixedUpdate` (gated at
   `game/Assets/Scripts/Runtime/TankController.cs:47`) never steps a networked tank. Also note
   `TankMotor.Step` approaches `topSpeed*mult` via `MoveTowards` (TankMotor.cs:37-45) — even a double-step
   could not exceed spec velocity; only extra `Physics.Simulate` calls or teleports can.
5. **ROOT CAUSE (a): DevArena is the match map and ships offline actors.** `LobbyState.BeginMatch` loads
   `SelectedMap` globally (LobbyState.cs:189-195, default `DevArena` via :61), and DevArena.unity contains
   an offline "Player" tank with `PlayerTankInput` at origin, 3 offline `BotBrain` bots, an ENABLED
   `FfaGameMode`, and enabled `PickupPad`s (`game/Assets/Editor/SceneBuilder.cs:91-113, 132-139, 150-165`).
   `CleanOfflineActors` (NetBootstrap.cs:124-138) is called only from StartServer/StartHost/client-connected
   (:62, :81, :106) — all long before this load. Nothing re-cleans after `SceneManager.OnLoadEnd`. Both the
   server and every client get a full parallel offline game. (Lobby.unity has no tanks, SceneBuilder.cs:185-218
   — which is why warmup feels fine and only match lives are affected.)
6. **ROOT CAUSE (b): reconcile replays multi-step unpaused offline rigidbodies.** Every reconcile replay
   iteration calls `TimeManager.SimulatePhysics(tickDelta)` on the shared physics scene
   (PredictionManager.cs:709-714). Networked objects are rolled back first; non-networked rigidbodies are
   NOT — FishNet ships `FishNet.Component.Prediction.OfflineRigidbody` precisely to pause them during
   reconciles (`Runtime/Generated/Component/Prediction/OfflineRigidbody.cs:98-106`). Our offline tanks lack
   it, so with reconciles ~every tick and 3-5 replayed ticks each, the client's ghost tanks integrate
   position at roughly 4-6x real time — the keyboard-mirroring ghost is the "out of control" tank, and its
   client-only collisions with the predicted tank corrupt replays into violent corrections (graphical
   teleports at the 5 u threshold). Server-side, its own offline copies are invisible-to-client colliders.
7. **Aggravators from the same root.** Offline `FfaGameMode.Start` registers ALL `TankController`s found —
   including networked ones spawned before its first frame (FfaGameMode.cs:36-41); its `Respawn` teleports
   roots (:122-137) and `EndRound` sets `InputFrozen` on registered tanks (:96-103), which the owner's
   `BuildMoveData` honors (NetworkTank.cs:105). Enabled PickupPads mutate weapons client-side. The
   lobby-match-lifecycle review (fact 2) prescribed `ReplaceOption.All` but missed that the match map
   itself re-introduces offline actors — this review supersedes that gap.

## Prescribed fix

- **Primary (small, exact):** in `NetBootstrap.EnsureHub()`, after hub creation subscribe once:
  `nm.SceneManager.OnLoadEnd += _ => { if (IsSessionActive) CleanOfflineActors(); };`
  FishNet's `SceneManager.OnLoadEnd` fires on server and client after every queued load. This restores the
  M3/M2c "offline actors step aside" invariant for every scene the session ever loads. One-frame residue
  (an offline FixedUpdate or FfaGameMode.Start before the coroutine fires OnLoadEnd) is harmless once the
  objects are destroyed/disabled; note it in a comment.
- **No TimeManager changes.** Defaults already clamp and resync (findings 1-2). Do not touch
  `_maximumFrameTicks`/`_allowTickDropping`; do not add manual elapsed-time resets.
- **Follow-up (backlog, not this fix):** generate network variants of match scenes without offline actors,
  or have `LobbyState` strip them server-side; and keep `OfflineRigidbody` in mind for any future
  intentionally-offline rigidbody that must coexist with a client session.

## Repro test (task step 3 judged)

The proposed displacement-bound probe is a weak primary: the ghost only misbehaves under reconcile replays
with real latency and needs input/contact — flaky in an in-process host. Replace with a deterministic
PlayMode assertion: run the real lobby flow (`DisableSceneManagement = false`), leader-start the match, and
after the map load assert (a) zero `TankController`s without a `NetworkObject`, (b) every `FfaGameMode`
disabled, (c) every `PickupPad` disabled — fails before the fix, passes after. Keep the graphical
displacement bound (`topSpeed*boostMult*frameDelta*safety`, teleports excluded) as a secondary regression
guard for both post-load windows.

## Deploy scope

Both images: client AND server (`bholcombe/potshot-*`). The server hosts its own ghost colliders and the
parallel FfaGameMode, so a client-only fix leaves invisible obstacles and the InputFrozen/round-end hazard
on tow-c1.
