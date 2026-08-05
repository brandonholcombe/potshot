# Review: ui-menus-hud.md

## Reviewer: sonnet-reviewer
Task: `Agents/TODO/Active/ui-menus-hud.md` (author: fable-primary). Verdict at end.

## Environment facts verified first

1. **Input handler: OK as designed.** `ProjectSettings/ProjectSettings.asset` has
   `activeInputHandler: 0` (legacy Input Manager only). StandaloneInputModule +
   EventSystem work; no Input System package is installed, so no conflict. No
   ProjectConfigurator change needed. PlayerTankInput/hotkeys all use legacy `Input.*`,
   consistent with the task's "legacy input" call.
2. **RuntimeInitializeOnLoad phrasing is wrong — must be fixed in design.**
   `[RuntimeInitializeOnLoadMethod]` fires ONCE per app session (after the first scene),
   not per scene. "A `PauseMenu` runtime component spawns itself via
   RuntimeInitializeOnLoad in non-menu scenes" cannot work as written: booting into
   MainMenu, the hook fires there and never again. Correct mechanism (prescribed):
   RuntimeInitializeOnLoad creates ONE persistent `DontDestroyOnLoad` controller (exact
   NetDevHotkeys precedent, `NetDevHotkeys.cs:13-19`) that subscribes to
   `SceneManager.sceneLoaded` and enables/disables the pause UI when the active scene
   is/isn't MainMenu. Prefer this over baking a component into every generated scene —
   SceneBuilder and MapImporter stay untouched and future imported maps are covered
   automatically. The pause panel itself should still be a UIFactory-generated prefab in
   Resources, loaded by the controller.

## uGUI / component availability

3. `com.unity.ugui` 2.0.0 is present (builtin, `packages-lock.json:94`); legacy `Text`,
   `InputField`, `Dropdown`, `Toggle`, `Slider` all exist in Unity 6's uGUI runtime —
   no TMP essentials import needed. Two practical notes: (a) Unity 6 has no builtin
   `Arial.ttf`; UIFactory must use `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")`
   for every Text/InputField or text renders blank. (b) Legacy `Dropdown` works but its
   programmatic template hierarchy (Template/Viewport/Content/Item + Toggle) is ~80 lines
   of fragile factory code for a 2-4 item map list. Recommend a cycle button
   ("Map: DevArena ◂▸") instead — simpler, same UX at this scope. Author's choice.
4. New `Potshot.UI` asmdef must explicitly reference `UnityEngine.UI` (asmdefs don't
   auto-link package asmdefs); same for `Potshot.EditorTools` (UIFactory) and any test
   asmdef touching UI types. Boundary direction is correct: UI → Core+Net; Net (verified,
   `Potshot.Net.asmdef`) references only Core + FishNet.Runtime — no cycle.

## Scene-0 swap — consumers checked

5. Explicit-args paths are safe: Docker entrypoint passes `-potshotServer`
   (`server/Dockerfile:16`), the M3 E2E uses explicit `-potshotClient potshot.kodloki.io`
   (m3 task line 42) — no script relies on the no-args default. `NetVersionTests.
   Startup_DefaultsToKodlokiInPlayerBuilds` MUST be updated to expect Menu; add a
   ResolveStartup case for the new Menu action.
6. **Guard the DevArena load — required on the server path, not an optimization.** On the
   server build MainMenu is excluded, so scene 0 IS DevArena. An unconditional
   `LoadScene("DevArena")` in the `-potshotServer` path schedules a reload for next frame
   while `StartServer()` runs immediately — PlayerSpawner's OnServerState spawns
   NetFfaState/bots into the about-to-die scene instance and the reload destroys them.
   Rule: load only if `SceneManager.GetActiveScene().name != target`. On the client build
   scene 0 is MainMenu, so `-potshotClient` genuinely loads DevArena then StartClient —
   correct, and load-then-connect ordering holds (connection handshake >> 1 frame).
7. FishNet tolerates local loads: with no global scenes the server sends
   `EmptyStartScenesBroadcast` and `OnClientLoadedStartScenes` fires post-auth regardless
   of the client's locally loaded scene (SceneManager.cs:560-590, 706) — today's flow
   already proves this; the menu changes nothing.
8. **PlaytestHotkeys F-keys: don't hardcode an offset.** `F1..F8 → LoadScene(i)` with
   MainMenu at index 0 would make F1 open the menu on client builds, but a fixed `i+1`
   is wrong on the server layout. Compute the base index by checking whether
   `SceneUtility.GetScenePathByBuildIndex(0)` is MainMenu. Esc: the task correctly says
   the pause menu "replaces the PlaytestHotkeys Esc=quit binding" — make that an explicit
   deletion of `PlaytestHotkeys.cs:28-29` (it ships on the tank prefab in ALL builds).
9. **NetDevHotkeys in the menu is a hazard the task doesn't cover.** H/J/K would start
   host/join while MainMenu is active (server spawns tanks into the menu scene), and its
   OnGUI status label overlaps the menu. Early-return both Update and OnGUI when the
   active scene is MainMenu.

## Networking design

10. FishNet APIs check out: `RegisterBroadcast<T>` defaults `requireAuthentication: true`
    (ServerManager.Broadcast.cs:41) — correct for NameBroadcast post-auth;
    `Broadcast<T>(T message, ...)` to all clients exists (ServerManager.Broadcast.cs:342)
    for the kill feed; `SyncDictionary<int, string>` is supported (string is a built-in
    serializer) and syncs to late joiners, which also covers the "late name" risk.
11. **Leave Match teardown has two latent bugs the menu flow will expose.**
    (a) `NetBootstrap.StartClient` (NetBootstrap.cs:50) subscribes a new anonymous
    `OnClientConnectionState` lambda on EVERY call and never unsubscribes — menu-driven
    join/leave/join accumulates handlers on the persistent hub. Use a named handler,
    unsubscribe or subscribe once. (b) `PlayerSpawner._botsSpawned` never resets on server
    stop — Host → Leave → Host yields a bot-less arena. Reset it on server-stopped state.
    Also: when hosting, Leave Match must stop BOTH ServerManager and ClientManager.
    Otherwise disconnect + LoadScene(MainMenu) is clean: FishNet despawns client objects
    on disconnect, hub survives via DDOL — no NetworkObject leakage beyond (a)/(b).
12. **Name sanitization: one pure function, used by both sides.** The task has the client
    validating (trim, ≤16) and the server sanitizing (trim, strip control chars, ≤16,
    fallback) — that's the same rule drifting in two places. Prescribe a single pure
    static, e.g. `Potshot.Net.PlayerNames.Sanitize(string raw, int clientId)` (Net, next
    to NameBroadcast), called by MenuController pre-send and by the server handler, with
    EditMode tests on the pure function only.

## Tests and scope

13. Test plan is realistic and matches NetCombatTests conventions (in-process host,
    port bump, physics-global restore). One fix: "pause menu toggles" can't inject legacy
    Esc input — expose a public `Toggle()` on the pause controller and test that.
14. Scope discipline is right: keep the scoreboard/kill feed as OnGUI text (identity
    slice repaints them); don't invest in uGUI HUD, fonts, or styling beyond flat
    panels — and don't build a server browser or Settings audio plumbing beyond the
    stored PlayerPrefs value.

## Verdict

**Approve with changes.** Required before implementation: #2 (persistent controller +
sceneLoaded, not per-scene RuntimeInitializeOnLoad), #6 (guard DevArena load on server
path), #8 (scene-offset by lookup + delete Esc-quit), #9 (NetDevHotkeys menu gating),
#11 (StartClient handler leak, `_botsSpawned` reset, host teardown), #12 (shared
sanitizer), #5 (update NetVersionTests). #3 (LegacyRuntime.ttf; cycle button suggested)
and #4 (asmdef refs) are implementation notes.
