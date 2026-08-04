# Review — m2a-fishnet-install-handshake.md

## Reviewer: sonnet-reviewer
Task: `Agents/TODO/Active/m2a-fishnet-install-handshake.md` (Author: fable-primary)

## Verdict: approve-with-changes

Plan shape is right (install → factory → bootstrap → authenticator → tests),
but the two highest-value facts change the plan: the license is NOT MIT-style,
and UPM is the better install path — it also makes the license question moot.

## Findings

1. **LICENSE: the task doc is wrong — FishNet free is NOT "MIT-style."** It is
   a custom FirstGearGames license (see repo `LICENSE.md`). §1.a grants
   "reproduce, modify, and use the Software for developed game[s]"; §2.b bars
   competing networking solutions; the free tier has **no explicit public
   redistribution grant** (only Pro gets enumerated distribution rights, §3.b).
   Committing the imported source to this PUBLIC repo is defensible-but-gray
   (the same source is already public on GitHub under the same license), and it
   collides with our own assets.md rule 4 ("if unclear, keep them out of git").
   Resolution: install via UPM (finding 2) so no FishNet source enters the repo
   at all. Fix the ledger row and task doc wording to "custom FirstGearGames
   license, free tier" — never "MIT."

2. **INSTALL PATH: use UPM git-URL, not the unitypackage.** The FishNet README
   officially documents Package Manager install, and the tag is pinnable:
   `"com.firstgeargames.fishnet": "https://github.com/FirstGearGames/FishNet.git?path=Assets/FishNet#4.7.2"`
   in `game/Packages/manifest.json`. Latest stable is **4.7.2** (2026-04-17,
   Unity min 2021.3 per its package.json; Unity 6 fully supported — the 4.6/4.7
   line carries the Unity 6 multithreading/ObjectCache fixes, so do not pin
   older). Benefits over `-importPackage`: nothing committed (package lives in
   Library/PackageCache; only manifest + `packages-lock.json` land in git — the
   lock records the exact resolved commit hash, which IS the "SHA-ish
   provenance" the task wants); no demo pruning needed in-repo; no batchmode
   import step; registry deps (`com.unity.nuget.newtonsoft-json`, ugui) resolve
   automatically. The release asset `FishNetworking.4.7.2R.unitypackage`
   (`https://github.com/FirstGearGames/FishNet/releases/download/4.7.2/...`)
   exists as documented fallback only. Update docs/netcode.md's install section
   (it currently prescribes the unitypackage/extract method). Note: FishNet
   auto-generates `DefaultPrefabObjects.asset` under `Assets/` when installed
   immutably — commit it.

3. **Authenticator design: matches the real 4.x API, one mechanic to pin
   down.** Confirmed from source: subclass `FishNet.Authenticating.Authenticator`
   (a MonoBehaviour), declare `public override event Action<NetworkConnection,
   bool> OnAuthenticationResult`, override `InitializeOnce(NetworkManager)` and
   there call `ServerManager.RegisterBroadcast<VersionBroadcast>(handler,
   requireAuthentication: false)` — the `false` is mandatory — plus
   `ClientManager.RegisterBroadcast<ResponseBroadcast>` and send the client's
   version on `OnClientConnectionState == Started`. **Kick-with-reason**: there
   is no reason parameter on the kick — the server sends a reason broadcast
   (e.g. `ResponseBroadcast { Passed, Message }`) FIRST, then invokes
   `OnAuthenticationResult(conn, false)`; FishNet kicks internally on the false
   result (PasswordAuthenticator demo does exactly this ordering). Also guard
   `conn.IsAuthenticated` re-broadcasts with `conn.Disconnect(true)`.
   ClientHost (H hotkey, PlayMode tests) still runs through the authenticator —
   host sends its own version and trivially passes; no HostAuthenticator bypass
   needed.

4. **Asmdef wiring**: FishNet's runtime asmdef name is exactly
   `FishNet.Runtime` (verified in repo). `Potshot.Net` references
   `["Potshot.Core", "FishNet.Runtime"]`. Per m0b conventions keep
   Potshot.Core clean of FishNet; add `Potshot.Net` (+ `FishNet.Runtime` where
   needed) to `Potshot.Tests.EditMode` / `Potshot.Tests.PlayMode` references.

5. **NetworkHub prefab-from-factory: sound, two specifics.** FishNet does not
   require a scene-placed NetworkManager; runtime instantiation works if it
   exists before anything networked. Have NetBootstrap instantiate it in
   `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` and only when a net mode
   is requested (offline stays FishNet-inert). Wiring without GUI: ServerManager
   falls back to `GetComponent<Authenticator>()` when its serialized
   `_authenticator` is null — put VersionAuthenticator on the same GameObject
   as NetworkManager/ServerManager and no serialized-field surgery is needed.
   Make NetworkFactory get-or-create idempotent like PrefabFactory. Do NOT add
   PlayerSpawner stubs now — M2b will tie spawning to FfaGameMode; an empty
   stub would just be churn.

6. **H/J hotkeys are in the wrong place if added to PlaytestHotkeys.** That
   component lives on the Tank prefab (`RequireComponent(WeaponController)`);
   in M2b the local tank won't exist until after connect+spawn, so host/join
   keys must outlive/precede tanks. Put them on NetBootstrap's persistent
   object (or a scene-level dev component), not the tank.

7. **PlayMode tests headless: viable, three pitfalls.** Tugboat is pure-socket
   (dedicated servers run `-nographics` routinely) so batchmode is fine. (a)
   Don't hardcode 7777 in tests — a stale process or parallel CI run collides;
   set a per-test port on Tugboat. (b) NetworkManager is DontDestroyOnLoad and
   has a Persistence rule — tests MUST stop client+server and destroy the hub
   in teardown or the second test trips over the survivor. (c) Bound every
   connect/kick assert with a frame-loop deadline (~5 s), not bare waits; a
   never-authenticating client is not auto-kicked promptly by default.

8. **Version-compare purity: good.** Static compare over `GameVersion.Version`
   strings ("0.1.0+dev" format) with equal/mismatch/malformed EditMode cases
   matches m0b test conventions. Treat malformed/empty as mismatch (fail
   closed). The "FishNet assemblies present" EditMode test should assert
   `Type.GetType`/assembly presence of `FishNet.Runtime`, which also guards the
   UPM resolve step in CI.

## Required changes

Ledger/doc license correction (1); UPM install pinned `#4.7.2` + netcode.md
rewrite (2); reason-broadcast-before-result kick ordering (3); hotkeys off the
tank prefab (6); test port/teardown/timeout discipline (7).
