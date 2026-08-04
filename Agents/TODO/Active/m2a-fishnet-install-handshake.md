# M2a — FishNet install, connection scaffolding, version handshake

## Author: fable-primary
## Status: Not Started

First netcode slice: FishNet in the project, a server/client connection
path, and the build-version handshake. Gameplay stays untouched (tanks
remain offline-driven until M2b).

## Plan

1. **FishNet install** (docs/netcode.md method): download the pinned
   `.unitypackage` from FirstGearGames/FishNet GitHub releases (pin the
   exact release tag in this doc at implementation; latest stable 4.x
   compatible with Unity 6). Import via batchmode `-importPackage` so
   `.meta` files come from Unity. Record version + SHA-ish provenance in
   docs/netcode.md. FishNet is free/open (MIT-style for the free tier) —
   ledger row in docs/assets.md.
2. **NetworkFactory (editor)**: generates `Assets/Resources/Prefabs/
   NetworkHub.prefab` — FishNet NetworkManager + Tugboat transport
   (port 7777) + our `VersionAuthenticator` wired. No GUI setup.
3. **NetBootstrap** (runtime, Potshot.Net asmdef — NEW assembly,
   references Potshot.Core + FishNet assemblies): reads CLI args /
   static config: `-potshotServer` starts server, `-potshotClient
   <host>` connects, neither = offline (current behavior unchanged).
   PlaytestHotkeys H = host (server+client), J = join localhost (dev
   loop convenience).
4. **VersionAuthenticator** (FishNet Authenticator subclass): client
   sends `GameVersion.Version` broadcast on connection; server compares
   and kicks mismatches with a reason message. Comparison logic lives in
   a pure static method (unit-testable without networking).
5. **Tests**:
   - EditMode: FishNet assemblies present; version-compare logic
     (equal/mismatch/malformed).
   - PlayMode (batchmode gate): start server + local client in-process
     (FishNet supports this), assert authenticated connection with
     matching version; assert a doctored client version gets kicked.
     These run headless — no graphics dependency.
6. Docs: netcode.md updated with actual install/version details.

## Explicitly deferred

- Tank/weapon/score networking → M2b/M2c.
- Headless multi-process harness → M2d.
- Menus/server browser → ui-menus-hud (backlog).

## Risks

- FishNet 4.x + Unity 6 compatibility — reviewer to verify the current
  recommended release before implementation.
- unitypackage import via batchmode pulls demo/example folders — prune
  to the runtime essentials if the package layout allows (keep the
  repo lean; document what was pruned).
- New asmdef (Potshot.Net) must reference FishNet's asmdefs by their
  actual names — verify post-import.
