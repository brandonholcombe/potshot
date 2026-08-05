# Review — match-start-transition.md

## Reviewer: sonnet-reviewer
Task: `Agents/TODO/Active/match-start-transition.md` (author: fable-primary).
FishNet 4.7.2 source cited from `game/Library/PackageCache/com.firstgeargames.fishnet@0728292d8339` (as `Runtime/...`).

## Verdict: approve-with-changes

**Fast bullets — the tick-catch-up hypothesis is dead, stronger than the tick-burst review stated: with
tick dropping ON, local simulation can NEVER render faster than wall clock.** The real mechanism is
NetworkTransform catch-up on shells the bots fire while the client is still loading the map. **Camera —
the task's swoop diagnosis is the minor variant; the primary defect is a race that skips the retarget
entirely, leaving the camera frozen for the whole first life.** Fix list at the end, ranked.

## Findings — timing (task item 2)

1. **Tick loop cannot fast-forward.** `IncreaseTick()` accumulates `Time.unscaledDeltaTime`
   (`Runtime/Managing/Timing/TimeManager.cs:700-702`); `ticksCount = floor(elapsed/delta)` (:705), and the
   dropping clamp (`_allowTickDropping=true` :171, `_maximumFrameTicks=3` :178) only REDUCES:
   `_elapsedTickTime = timePerSimulation * 3` (:710-714). Simulated time per frame ≤ that frame's real
   elapsed time, always. After a 1-3 s stall: 30-90 ticks owed; exactly 3 simulated (in a frame that itself
   took ≥1 s — rendered as SLOW, not fast), 27-87 dropped. At steady 60 or 120 fps, ticksCount is 0-1 per
   frame — **the fast-forward window is zero-length at both frame rates**. Hub defaults confirmed unchanged
   (`game/Assets/Resources/Prefabs/NetworkHub.prefab`: `_allowTickDropping: 1`, `_maximumFrameTicks: 3`,
   `_tickRate: 30`).
2. **Client resync is a jump, not simulation.** `ParseTimingUpdate` (TimeManager.cs:1177-1230, sent every
   `TimingTickInterval = _tickRate` ticks = 1 s, :102,1145) assigns `Tick = (LocalTick-clientTick)/2 +
   lastPacketTick + 1` directly; error >4 ticks resets `_adjustedTickDelta = TickDelta`; otherwise a ±1 %
   nudge (`ChangeAdjustedTickDelta`, :1130-1137). Max sustained speed skew: 1 %. Not perceivable.
3. **Mitigation (a) — post-load input freeze: REJECT for this symptom.** The bullets are bot shells and NT
   playback, not inputs; the local catch-up window is zero (finding 1). (Tick delta IS client-knowable —
   `TimeManager.Tick` vs `LastPacketTick.RemoteTick` — but there is nothing to gate.) Mitigation (b) —
   TimeManager config: nothing to change; defaults already drop-and-jump. Mitigation (c) — accept: YES for
   tick mechanics, but the symptom is real and comes from findings 4-5.
4. **REAL MECHANISM 1 — NT first-goal catch-up glide on shells spawned around the client's load.**
   `LobbyState.OnSceneLoadEnd` spawns bots + tanks the moment the SERVER load ends (LobbyState.cs:227-251),
   seconds before clients finish their (asset-heavy) load; no observer SceneCondition exists (NetworkFactory
   adds no ObserverManager conditions), so spawns/NT data reach clients mid-load. Bots fire on sight at
   other bots (BotBrain.cs:82-95). For a client shell whose earlier NT packets were lost or that just
   spawned: first goal's `prevTd.Tick==0` → `GetTickDifference` clamps to **1 tick**
   (`Runtime/Generated/Component/NetworkTransform/NetworkTransform.cs:2240-2257`), and
   `SetCalculatedRates` sets `rate = distance/timePassed` (:2129-2131) — the shell's entire accumulated
   flight rendered at N× muzzle speed over 33 ms. ProjectileNet leaves `_enableTeleport` at its default
   **false** (NetworkTransform.cs:401; PrefabFactory.cs:131-160 sets only clientAuth/componentConfiguration),
   so the teleport escape (:2110-2118) never triggers — it MUST glide. A 20 u/s shell 1 s into flight
   replays 20 u in one tick ≈ 600 u/s. That is Brandon's "bullets far too fast".
5. **REAL MECHANISM 2 — burst trim+snap strobing.** When ≥6 buffered NT updates apply in one frame
   (any >200 ms load hitch), the queue overflow path drops to `_interpolation` (2) and SNAPS
   (`SetInstantRates`+`SnapProperties`, NetworkTransform.cs:2425-2440) — shells pop forward in 100-200 ms
   jumps during the hitchy window. Normal playback is real-time (delta-based goal consumption, :1670-1699;
   catch-up multiplier only +5 %, :1642-1645), so mid-match shells are fine — matching the symptom being
   match-start-only.

## Findings — camera (task item 3)

6. **PRIMARY: the retarget race — frozen camera for the whole first life.** FishNet unloads replaced scenes
   BEFORE loading new ones (`Runtime/Managing/Scened/SceneManager.cs:1250-1262` unload loop completes, load
   loop starts :1264+). The server spawns the owned tank at ITS OWN load end without awaiting client
   presence (LobbyState.cs:236-241 — deviating from lobby-match-lifecycle review fact 4, which prescribed
   `OnClientPresenceChangeEnd` for player tanks), so the spawn applies on the client mid-load →
   `NetworkTank.OnStartClient` runs when NO CameraFollow exists (old camera destroyed, new not loaded) →
   `FindFirstObjectByType<CameraFollow>()` returns null and the retarget is silently skipped
   (NetworkTank.cs:70-76, one-shot). The map camera then targets the baked offline "Player"
   (MapImporter.cs:375-382; SceneBuilder.cs:167-175), which `CleanOfflineActors` destroys
   (PlayerSpawner.cs:45-49 → NetBootstrap.cs:129-135) → `target==null` early-return freezes it
   (CameraFollow.cs:15) until the first death/respawn re-runs OnStartClient. Initial join is unaffected
   (warmup spawn waits for `OnClientLoadedStartScenes`) — this hits mid-session replaces only, i.e. match
   start and return-to-warmup. The task's swoop (exp-smoothing at 10/s ≈ 0.3 s cross-map sweep) is the
   secondary case, seen on respawns and whenever the race is lost the other way.
7. **SnapTo prescription.** Add `CameraFollow.SnapTo(Transform t)` = `{ target = t; transform.position =
   t.position + offset; }` — snaps the CAMERA only; the Graphical child stays owned by the prediction
   smoother (task risk honored). Replace the one-shot OnStartClient retarget with an owner-side poll (in
   `NetworkTank.Update`, owner only): while `follow == null || follow.target != graphical`, find CameraFollow
   and `SnapTo(graphical)` — closes the race for match start, return-to-warmup, and respawns in one code path.
8. **Zero-camera window: real, seconds long.** Between unload end and load end (finding 6 ordering) the
   client has NO camera at all — Unity renders nothing (black + "No cameras rendering"). DDOL survivors
   (hub, overlay/pause controllers) carry no camera. Cheapest mask: a full-screen "Loading…" panel on the
   DDOL LobbyOverlayController, shown between client-side `SceneManager.OnLoadStart`/`OnLoadEnd` —
   ScreenSpaceOverlay canvases render with zero cameras. Optional but recommended; it also hides finding 6's
   residue.
9. **AudioListener: no duplication — there are ZERO listeners anywhere.** All 7 scenes grep 0 for
   AudioListener; no builder adds one (SceneBuilder.cs cameras, MapImporter.cs:375-381, UIFactory MenuCamera
   :103-105); no prefab has one. Nothing fights; audio is simply inert. Latent only — add a listener to the
   follow camera when audio ships. No action this task.

## Transition checklist (task item 4)

10. **EventSystem continuity — BROKEN (this is dead-overlay-buttons round three).**
    `LobbyOverlayController.Show()` creates its EventSystem only when `_panel == null`
    (LobbyOverlayController.cs:49-60); the panel is DDOL (`Instantiate(prefab, transform)`, :61) and
    survives every replace, but the EventSystem is scene-local and dies with the first swap → from match
    start onward NO EventSystem exists (game scenes ship none) until someone opens the pause menu
    (PauseMenuController.cs:62-69). Round-2 warmup leader buttons are dead. FIX: ensure the EventSystem in
    `Update()` while the panel is shown (keep it scene-local per the pause-menu rule). Extend
    LobbyLifecycleTests — it asserts the EventSystem only in the FIRST warmup (LobbyLifecycleTests.cs:126-131).
11. **DevHud — OK (flicker accepted).** Lives on the tank prefab, owner-only (remote copies destroyed,
    NetworkTank.cs:80-82); dies at each despawn, returns at spawn; absent during loads. Cosmetic.
12. **Pause menu — OK.** DDOL controller closes on every sceneLoaded (PauseMenuController.cs:26-29);
    panel and wiring recreated per open; renders without a camera (overlay canvas).
13. **Pre-join status panel — OK.** Scene-baked in MainMenu (MenuController.cs:43-53); ReplaceOption.All
    unloads the menu on first global load, destroying it. No orphan.
14. **Projectiles/tracers across swaps — OK.** Server copies die with the unloading scene; NetworkObject
    routes the despawn (lobby-lifecycle review fact 5). Client copies are moved to the MovedObjects holder
    pre-unload (`_moveClientObjects`, SceneManager.cs:1233-1248) and then despawned by the server message —
    not orphaned into the new scene. Owner-local tracers (plain Projectile, no NetworkObject) are destroyed
    with the scene. PostMatch has no scene change; shells finish flight with inputs frozen (bots included —
    BuildMoveData reads the synced phase for all controllers, NetworkTank.cs:102-112).
15. **NetDevHotkeys — minor nits, no action required.** DDOL; H/J/K have no session guard (re-pressing
    mid-session calls StartConnection on live managers — FishNet no-ops with a warning), and the status
    line claims `potshot.kodloki.io` whatever host you actually joined (NetDevHotkeys.cs:36-41). Backlog.
16. **PickupPads/FfaGameMode — OK (covered).** Re-disabled after every load (PlayerSpawner.cs:45-49);
    regression-tested (LobbyLifecycleTests.cs:150-161). Aim raycast is Camera.main-null-safe during camera
    gaps (PlayerTankInput.cs:33-39).

## Required changes (ranked by user impact)

1. Camera: `CameraFollow.SnapTo` + owner-side polling retarget (findings 6-7). Client-only.
2. Fast bullets: match-start grace — defer `SpawnBots` (or gate bot fire) until all connected clients have
   arrived in the map (`OnClientPresenceChangeEnd` roster check, per lobby review fact 4) so no shells fly
   while anyone is loading; AND set `_enableTeleport=true`, `_teleportThreshold≈2` on ProjectileNet in
   PrefabFactory (mirrors TankNet's :111-113) so any residual catch-up snaps instead of gliding at 30x.
   Server change ⇒ deploy both images.
3. EventSystem: per-frame ensure in LobbyOverlayController while visible (finding 10). Client-only.
4. Loading overlay masking the zero-camera window (finding 8). Client-only, may ship separately.
5. Tests (task item 4): extend LobbyLifecycleTests — after match load AND after return-to-warmup: camera
   within X u of the owned tank within N frames; EventSystem present in round-2 warmup; zero
   non-networked TankControllers (exists); no NetworkProjectile whose gameObject.scene is unloaded.
6. NO TimeManager changes (findings 1-3) — do not touch `_maximumFrameTicks`/`_allowTickDropping`.
