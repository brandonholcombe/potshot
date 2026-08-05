# UI — Main menu, pause menu, named scoreboard

## Author: fable-primary
## Status: Not Started

Brandon's decisions (2026-08-04, AskUserQuestion): name entry YES;
scope STANDARD (no server browser yet); mouse/keyboard only.
Visual identity/juice is the NEXT slice — this pass is structure with
clean-but-plain styling (dark panels, light text; legacy uGUI Text, no
TMP essentials dependency).

## Design

1. **Boot flow**: new generated scene `MainMenu` becomes build index 0.
   NetBootstrap.ResolveStartup gains a `Menu` action: no-args player
   builds now SHOW THE MENU (the kodloki auto-connect moves onto the
   Play Online button as the default-highlighted action). Explicit CLI
   args bypass the menu entirely (server/CI/E2E flows unchanged):
   -potshotServer → load DevArena + StartServer; -potshotClient <host> →
   load DevArena + StartClient; -potshotOffline → load DevArena offline.
   PlaytestHotkeys F-keys shift to scenes 1+ (menu excluded).
2. **UIFactory (editor)**: ALL UI generated programmatically (zero-GUI
   rule): Canvas + CanvasScaler (1920x1080 reference), EventSystem +
   StandaloneInputModule (legacy input, matches PlayerTankInput), dark
   panel + buttons via flat Image + legacy Text.
   - **MainMenu scene**: title "POTSHOT"; name InputField (persisted
     PlayerPrefs `potshot.playerName`, default "Tanker"); buttons:
     Play Online (kodloki default host), Host (server+client), Join
     (address InputField), Offline vs Bots (map dropdown: DevArena +
     imported maps), Settings, Quit. Settings panel: fullscreen toggle,
     volume slider (stored `potshot.volume`, applied to AudioListener —
     audio arrives with the juice slice), back.
   - **PauseMenu prefab**: Esc toggles in game scenes (replaces the
     PlaytestHotkeys Esc=quit binding): Resume / Leave Match (disconnect
     + load MainMenu) / Quit. A small `PauseMenu` runtime component
     spawns itself via RuntimeInitializeOnLoad in non-menu scenes.
3. **MenuController** (new `Potshot.UI` asmdef referencing Core + Net):
   wires buttons to NetBootstrap (Play Online = LoadScene(DevArena) +
   StartClient(DefaultHost); Host = LoadScene(map) + StartHost; Join =
   LoadScene(DevArena) + StartClient(address); Offline = LoadScene(map)
   only). Address + name validated (trim, length ≤ 16).
4. **Player names over the wire**: after authentication the client sends
   `NameBroadcast { Name }`; server sanitizes (trim, strip control
   chars, ≤16, fallback "Tanker<ClientId>"), stores conn→name;
   NetFfaState gains `SyncDictionary<int, string> Names` (same keys as
   Kills; bots = "Bot N"); scoreboard OnGUI shows names; plus a minimal
   **kill feed** (last 4 lines, "A potshotted B" / "B potshotted
   themselves", server broadcast, client-side timed fade) — visual
   dial-in of both comes with the identity slice.
5. **Tests**: EditMode — UIFactory scene generation asserts (canvas,
   buttons, input fields findable by name); name sanitization pure
   cases. PlayMode — in-process host: NetFfaState.Names populated from
   a sent NameBroadcast; kill feed line recorded on a scripted kill;
   pause menu toggles. Post-deploy E2E: headless -potshotClient
   (menu bypass) still authenticates.
6. **Builder**: client scene 0 = MainMenu; server build EXCLUDES
   MainMenu (headless boots straight into DevArena).
7. Deploy: rebuild+push server, rollout, rebuild Mac app, E2E.

## Risks

- Name broadcast races the scene-load tank spawn — NetFfaState tolerates
  late names (defaults "Player<id>" until the broadcast lands).
- Menu-driven scene loads preserve the existing load-then-connect order
  (OnClientLoadedStartScenes semantics unchanged).
- Legacy input module required (project runs legacy Input already).
