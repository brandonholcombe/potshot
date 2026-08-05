# Review: ui-prejoin-status.md

## Reviewer: sonnet-reviewer
Task: `Agents/TODO/Active/ui-prejoin-status.md` (author: fable-primary). Verdict at end.

## THE blocker: insecureHttpOption

1. **`ProjectSettings.asset:689` has `insecureHttpOption: 0` (NotAllowed) — every
   plain-http UnityWebRequest in this design fails with `InsecureConnectionNotAllowed`
   before any packet is sent.** Verified against the local Unity 6000.1 docs
   (`InsecureHttpOption.NotAllowed`: "UnityWebRequest uses HTTPS connections at all
   times. This is the default"). No localhost exemption is documented, so the PlayMode
   tests against `http://localhost:<port>/status` fail too, as does the editor.
   Required: add to `ProjectConfigurator.Configure()` (game/Assets/Editor/
   ProjectConfigurator.cs) `PlayerSettings.insecureHttpOption =
   InsecureHttpOption.AlwaysAllowed;` (serializes as 2). It must be AlwaysAllowed, not
   DevelopmentOnly (1): the Mac client Brandon runs is a non-development build.
   Document in the task/docs that M4's TLS route is the trigger to tighten this back.

## Environment risks verified

2. **macOS ATS: non-issue, drop the TcpClient fallback plan.** The Unity 6000.1.14f1
   mac player Info.plist template (`PlaybackEngines/MacStandaloneSupport/Source/Player/
   MacPlayer/MacPlayerEntryPoint/Info.plist`) ships `NSAppTransportSecurity →
   NSAllowsArbitraryLoads = 1`. Only #1 gates plain http on the Mac.
3. **HttpListener: available.** Project is `apiCompatibilityLevel: 6` (.NET Standard
   2.1); the bundled `NetStandard/ref/2.1.0/netstandard.dll` exports HttpListener and
   Mono supplies the implementation in player builds (Linux and Mac; no IL2CPP here).
   Mono's HttpListener is fully managed (no Windows http.sys/URLACL), so
   `"http://*:8080/"` needs no privileges on Linux for a port >1024. Use `*`, not `+`.
4. **Stop() makes the blocked `GetContext()` throw** (ObjectDisposedException/
   HttpListenerException in Mono) — wrap the accept loop in try/catch that exits
   cleanly, and set `thread.IsBackground = true`. Required, not optional.
5. **Thread lifetime: server-stop event alone is not enough.** UiNetTests-style
   teardown does call `ServerManager.StopConnection(true)` (so the Stopped event fires),
   but a test that fails before teardown, or an editor domain reload, leaks the thread
   holding the port. Stop the listener from `OnServerConnectionState(Stopped)` AND
   `OnDestroy` AND `OnApplicationQuit`. Also wrap `listener.Start()` in try/catch:
   a bind failure (port taken — e.g. host mode on the Mac) must log and degrade, never
   take down the game server.

## Design corrections

6. **"Volatile snapshot struct" is not a thing in C# — a multi-field struct field
   cannot be `volatile` and reads can tear.** Simplest correct pattern: the main-thread
   1 Hz tick builds the complete JSON string and swaps it into a `volatile string`
   field; the listener thread only reads the reference (atomic) and writes bytes.
   This also moves the hand-rolled JSON onto the main thread where it's testable.
7. **Player count: filter for authentication.** `ServerManager.Clients` entries are
   added at transport connect, before the version handshake (ServerManager.cs:622
   "Do nothing else until the client sends it's version"). Use
   `Clients.Values.Count(c => c.IsAuthenticated)` (`NetworkConnection.IsAuthenticated`,
   NetworkConnection.cs:113). Bots: report the config `PlayerSpawner.botCount` — a live
   BotBrain count dips during the 2 s respawn windows for no reader benefit.
8. **Version check: reuse `VersionAuthenticator.VersionsMatch`** (exact-equality,
   VersionAuthenticator.cs:73-74) rather than a second `!=` in MenuController, so the
   panel's red warning can never disagree with what the authenticator will do.
9. **NetworkHub prefab is generated** — PotshotStatus must be added in
   `NetworkFactory.CreateNetworkHubPrefab()` and the prefab regenerated; same for the
   StatusPanel in `UIFactory.BuildMainMenuScene()` + scene re-save. Follow the existing
   conventions exactly: elements found via `transform.Find("StatusPanel/...")`, so
   factory names and MenuController paths must match (MenuController.cs:108-112
   `Find`/`Wire` throw NullReference on mismatch — that is the existing, accepted
   failure mode). PlayOnlineButton rewires from direct `Launch` to opening the panel;
   run the status coroutine on MenuController (main thread, `request.timeout = 4` is a
   real UnityWebRequest property, whole seconds — fine).
10. **HTTP port must be injectable for tests** (mirror `Tugboat.SetPort` /
    `_nextPort++` in UiNetTests.cs:17,35): a public field/setter on PotshotStatus set
    before `StartConnection`. Default 8080. Without this, every existing server-starting
    PlayMode suite (NetCombat, UiNet, ...) starts fighting over one port.
11. **JOIN ANYWAY is sane**: it is the existing `StartClient(DefaultHost)` path, which
    already degrades to the offline bot game on connect failure (NetBootstrap.cs:60-62).
    Keep BACK from re-fetching stale state on reopen (re-run the check each open).

## K8s / deploy

12. Add to the SAME container's `ports:` in `K8s/deployment-server.yaml`:
    `containerPort: 8080, hostPort: 8080, protocol: TCP` alongside the UDP 7777 entry.
    `imagePullPolicy: Always` already set; `Recreate` strategy already covers the
    second hostPort. No plausible node clash: ingress-nginx sits behind the LB on
    NodePorts (30000-32767), kubelet uses 10250/10256 — nothing standard binds 8080.
    One deploy-time verify: confirm no Linode Cloud Firewall blocks 8080/tcp inbound
    (UDP 7777 already passes the 1:1 NAT, so likely none). The E2E curl covers this.
13. `docs/deployment.md:24-30` (split-host note) should gain one line: interim direct
    `potshot.kodloki.io:8080` until the M4 `status.potshot.kodloki.io` TLS route —
    which is also when finding #1 gets revisited.

## Verdict

**Approve with changes.** Required: #1 (insecureHttpOption=2 via ProjectConfigurator —
without it nothing works, including the tests), #4+#5 (guarded accept loop, background
thread, stop on Stopped/OnDestroy/OnApplicationQuit, guarded Start), #6 (volatile
prebuilt-JSON string, not a "volatile struct"), #7 (IsAuthenticated filter), #10
(injectable HTTP port). #2 removes the ATS fallback work; #3 confirms HttpListener with
the `*` prefix; #8, #9, #11-#13 are implementation notes to follow as written.
