# Review: lobby-match-lifecycle.md

## Reviewer: sonnet-reviewer
Task: `Agents/TODO/Active/lobby-match-lifecycle.md` (author: fable-primary).
FishNet 4.7.2 source at `game/Library/PackageCache/com.firstgeargames.fishnet@0728292d8339`
(cited below as `Runtime/...`). Verdict at end.

## Verdict: approve-with-changes

## Part A — FishNet SceneManager facts (source-verified)

1. **API confirmed.** `SceneManager.LoadGlobalScenes(SceneLoadData)` is server-only
   (`Managing/Scened/SceneManager.cs:893-912`; `CanExecute` requires only `IsServerStarted`,
   `:2532-2543`). `new SceneLoadData("Lobby")` by name is supported
   (`LoadUnloadDatas/SceneLoadData.cs:48`); set `sld.ReplaceScenes = ReplaceOption.All`
   (`SceneLoadData.cs:29`, `ReplaceOption.cs:8-19`). Stacking is rejected for globals
   (`SceneManager.cs:904-908`). Loads are always Unity-additive under the hood (`:1268-1272`).
2. **ReplaceScenes semantics — must be `All`, and DDOL survives.** On a global replace the
   server swaps `_globalScenes` to the new names BEFORE unloading (`:1042-1045`), then unloads
   every scene except MovedObjects, the requested scenes, current globals, and manual-unload
   scenes (`:1169-1220`). Crucially, **offline scenes (client's locally loaded MainMenu, server's
   boot DevArena) are only unloaded when ReplaceOption.All** — `OnlineOnly` skips them
   (`:1211-1217`), leaving the menu (camera, EventSystem, MenuController) alive under the lobby.
   The DDOL pseudo-scene is never in `UnitySceneManager.GetSceneAt` iteration (`:1172`), so the
   hub and PauseMenuController survive. Prescribe `ReplaceOption.All` everywhere.
3. **Server-before-clients load is legal.** The completion broadcast just iterates currently
   connected clients — zero at boot (`:1483-1487`). Every later client receives the current
   globals at authentication (`OnClientAuthenticated`, `:509-582`). Boot sequence
   DevArena → `LoadGlobalScenes(Lobby, All)` immediately after ServerManager Started is fine;
   DevArena is unloaded as an offline scene (see fact 2), so nothing may be spawned into it first.
4. **OnClientLoadedStartScenes fires ONCE per connection** (`connection.SetLoadedStartScenes`,
   `:499-503`), server-side only after the client confirms loading the globals
   (`OnClientLoadedScenes` → `AddConnectionToScene` → `TryInvokeLoadedStartScenes`, `:670-712`).
   So (a) for joins it fires AFTER the joiner finished loading Lobby-or-map — PlayerSpawner's
   existing hook stays correct for join-time spawning; (b) it NEVER re-fires on mid-session
   replaces. The per-swap, per-client completion event is **`OnClientPresenceChangeEnd`**
   (`:245`, invoked from `AddConnectionToScene` `:1946-1948`); server-local completion is
   `OnLoadEnd` with `SceneLoadEndEventArgs`/`LoadQueueData` (`:229`, `:757-767`). Use
   OnLoadEnd(qd.AsServer) to spawn NetFfaState/bots, OnClientPresenceChangeEnd to spawn each
   player's tank. **Race:** a client authenticating while a global load is in flight gets an
   EmptyStartScenesBroadcast (`GlobalScenesExcludingLoading`, `:518-525,553-562`) and
   OnClientLoadedStartScenes fires with NO scene loaded — PlayerSpawner must consult LobbyState
   and defer to presence-in-scene rather than spawning unconditionally.
5. **Spawned objects on replace.** Only `SceneLoadData.MovedNetworkObjects` are preserved
   (`:1116-1124` → `:1377-1385`). Everything else in an unloading scene is Unity-destroyed;
   `NetworkObject.OnDestroy` does route despawns to clients
   (`Object/NetworkObject/NetworkObject.cs:422-448`) so it's not a desync, but it is the dirty
   path — the plan's despawn-tanks-BEFORE-load order is correct, keep it. `Instantiate` puts
   objects in the active scene, so a non-global LobbyState/NetFfaState dies with it. Prescribe:
   call **`nob.SetIsGlobal(true)` immediately after Instantiate** (must precede spawn,
   `NetworkObject.cs:215-247`) → FishNet itself moves it to DDOL (`:590-591`) on BOTH server and
   clients. Do this for both singletons; keep their lifetimes explicit (LobbyState forever,
   NetFfaState despawned at PostMatch end). Host mode is covered by `MoveClientHostObjects`
   (`SceneManager.cs:1810-1880`, `_moveClientObjects` default true `:308-313`); pure clients move
   surviving spawned objects across the swap automatically (`:1236-1248`).
6. **Physics: no special handling.** Loads use `LocalPhysicsMode.None` (`:1268-1272`,
   `LoadOptions.cs:25`) — one shared physics world, which the runtime-set TimeManager physics
   mode already simulates. Do not set `Options.LocalPhysics`. First global becomes the active
   scene automatically (`SetActiveScene`, `:2383-2439`).

## Part B — design findings

7. **Build lists + empty EditorBuildSettings (blocking).** `DefaultSceneProcessor.BeginLoadAsync`
   calls `UnitySceneManager.LoadSceneAsync(sceneName)` (`DefaultSceneProcessor.cs:71`); for a
   scene not in build settings that returns null and line 77 NREs. `ProjectSettings/
   EditorBuildSettings.asset` has `m_Scenes: []` today. Required: add Lobby.unity to BOTH
   `Builder.BuildMacDev` and `BuildLinuxServer` lists (`Editor/Builder.cs:19-23,43-47`), and add
   MainMenu/DevArena/Lobby/Maps to EditorBuildSettings via an editor script (QaProbe excluded) —
   this is also what makes the lifecycle testable in PlayMode and fixes MenuController's
   currently-empty in-editor map list (`UI/MenuController.cs:33-37`).
8. **State machine placement ✓** — spawned singleton via Instantiate+Spawn like NetFfaState
   (m2c rule: no NetworkObject on the hub, `Managing/NetworkManager.cs:312-313`). Spawn it in
   PlayerSpawner's Started handler; it drives the Lobby load from `OnStartServer`.
9. **Warmup invulnerability:** add `public bool invulnerable` to `Damageable` with an early
   return in `TakeDamage` (`Scripts/Runtime/Damageable.cs:30`). It's a plain engine-side flag —
   no netcode leak into Core, offline untouched, directly testable. "Don't wire damage in lobby"
   would need prefab/scene variants — rejected as more moving parts for less honesty.
10. **Mid-match join ✓** — `NetFfaState.Track` is idempotent on Kills/Names
    (`Net/NetFfaState.cs:88-96`) and OnClientLoadedStartScenes fires after the joiner loads the
    current global map (fact 4a). Route the spawn decision through LobbyState.
11. **Lobby scene spawn points (bug in plan):** with no FfaGameMode, `NetFfaState.PickSpawn`
    collapses to `(0, 0.5, 0)` (`NetFfaState.cs:101-108`) — and NetFfaState won't exist in
    warmup at all. BuildLobbyScene must emit a spawn ring (LobbyState-read, or a disabled
    FfaGameMode for uniformity) or all warmup tanks stack at origin.
12. **Countdown:** default SyncVar send rate is 0.1 s (`Object/Synchronizing/
    SyncTypeSetting.cs:13,42`), so a per-frame float is 10 Hz churn. Prefer syncing a single
    `SyncVar<uint>` end-tick set once at Countdown entry; clients render remaining via
    `TimeManager.TicksToTime` (`Managing/Timing/TimeManager.cs:871-909`). State enum + settings
    as plain SyncVars are fine.
13. **Test adaptation — one mechanism:** do NOT add a "no LobbyState → legacy spawn" bypass in
    PlayerSpawner; it keeps a dead production path and would silently mask a missing LobbyState
    live. Instead tests drive the REAL flow: first client is leader, test issues StartMatch, with
    a public `countdownDuration`/`postMatchDuration` on LobbyState set to ~0 pre-start (same
    pattern as `PlayerSpawner.botCount` today). Provide one shared test helper coroutine
    (StartHostToMatch) used by NetMoveTests/NetCombatTests/SmoothingTests/UiNetTests; requires
    finding 7's EditorBuildSettings work. Warmup-invulnerability and non-leader-ignored tests
    fall out of the same flow.
14. **Leader migration testability:** one client per ClientManager — a second in-process
    connection requires a second NetworkManager instance (FishNet multi-instance works, and
    scene handling degrades acceptably since the scene is already loaded, `SceneManager.cs:
    2098-2123`), but the second hub duplicates PlayerSpawner/NameSync/static events and every
    `FindFirstObjectByType` in tests becomes ambiguous — brittle; don't build it in this slice.
    Extract leader selection/migration into a pure `LobbyRules` (FfaRules pattern), EditMode-test
    it, integration-test only "first client is leader," and cover migration in the live E2E.
15. **LobbyOverlay placement:** not baked into the Lobby scene — countdown and PostMatch
    standings must render in map scenes too. Prescribe a DDOL client controller (PauseMenuController
    pattern, `UI/PauseMenuController.cs:18-24`) that instantiates a UIFactory-generated
    `Resources/UI/LobbyOverlay` prefab and shows/hides panels off LobbyState presence + state.
16. **NetBootstrap client path:** Play Online must call `StartClient` directly from MainMenu
    (drop `RunInGameScene`/`Launch` scene preload for online; `Net/NetBootstrap.cs:177-178`,
    `UI/MenuController.cs:46-53`); the server's global load replaces the menu (fact 2). Keep
    DevArena boot for `-potshotServer` (scene 0) and `-potshotOffline` unchanged.
17. **Scope:** borderline. Acceptable as one slice ONLY implemented server-first (state machine +
    scene flow + spawn rework + test adaptation, with a temporary OnGUI readout per the
    NetFfaState precedent) and UI overlay last; if anything slips, split the UI into its own
    task rather than shipping an untested lifecycle.

## Required changes (summary)
ReplaceOption.All everywhere (2); spawn via OnLoadEnd + OnClientPresenceChangeEnd for mid-session
swaps, guard the empty-broadcast auth race (4); SetIsGlobal(true) on both spawned singletons (5);
build lists + EditorBuildSettings population (7); lobby spawn ring (11); end-tick countdown (12);
leader-driven test helper, no PlayerSpawner bypass flag (13); pure LobbyRules for migration (14);
DDOL overlay controller (15); direct-connect from menu (16).
