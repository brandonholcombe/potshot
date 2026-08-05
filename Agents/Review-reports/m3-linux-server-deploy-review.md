# Review — m3-linux-server-deploy.md

## Reviewer: sonnet-reviewer
Task: `Agents/TODO/Active/m3-linux-server-deploy.md` (Author: fable-primary)

## Verdict: approve-with-changes

Plan is deployable as written except for one design contradiction (finding 1)
and one real script bug (finding 5). Environment is fully green — no blockers.

## Environment verification (all read-only, 2026-08-04)

- **Linux server module: PRESENT.** `/Applications/Unity/Hub/Editor/6000.1.14f1/PlaybackEngines/LinuxStandaloneSupport/Variations/`
  contains `linux64_server_{development,nondevelopment}_mono` (+ il2cpp). Mono server subtarget will build.
- **Docker: running** (client 28.3.3, Desktop engine 29.4.2, buildx v0.33.0). Builder `multiarch`
  (docker-container, running) and the default builders all offer `linux/amd64`. amd64 image production confirmed possible.
- **tow-c1 reachable** via `~/.kube/linode-config`; 3 Ready nodes:
  `lke484433-700897-00b7001f0000` (ext 172.234.239.87), `lke484433-700897-0177a9ae0000` (ext 172.234.250.111),
  `lke484433-873934-00207c9d0000` (ext 172.234.239.122). Note: INTERNAL-IPs are `192.168.15x.x`, not the
  `172.31.x` claimed in deployment.md/memory — cosmetic here (external IPs are what matter), but fix the doc.
- **Secrets:** `~/.config/eloup-wizard/secrets.json` has `dockerhub_pat` and `linode_pat` keys. Values not read.
- **DNS:** `kodloki.io` zone live on Linode NS (ns1–ns5.linode.com); apex A = 172.232.176.47 (ingress LB).
  `potshot.kodloki.io` currently has **no A record** — the step-5 call is a CREATE (POST), not an update.

## Findings

1. **DNS host contradiction (deployment.md) — resolve NOW.** deployment.md routes the M4 HTTP status endpoint
   through the ingress LB (172.232.176.47) at `potshot.kodloki.io`, while M3/M4 point the SAME name's A record at
   the labeled node's public IP for game UDP. One name cannot resolve to both; with the A record on the node,
   ingress-nginx (behind the LB Service, not hostNetwork) never sees the HTTP traffic and cert-manager's HTTP-01
   solver for that host will also fail. **Required:** split hosts — `potshot.kodloki.io` → game node IP (M3);
   status/download page at e.g. `status.potshot.kodloki.io` (or `potshot-status.kodloki.io`) → 172.232.176.47 via
   ingress. Update deployment.md and K8s/README.md in this milestone so M4 doesn't inherit the contradiction.
2. **Negative-DNS-cache trap on the new record.** The zone SOA negative TTL is 86400s (24 h). Any resolver that
   queried `potshot.kodloki.io` before the record exists (this review's dig did) may cache NXDOMAIN for hours.
   Order step 5 before step 6, and run the first E2E against the node public IP directly; use
   `dig @ns1.linode.com potshot.kodloki.io` to confirm the record, not the local resolver.
3. **Step-1 cleanup: blast radius is acceptable, but the camera MUST be re-targeted.** Destroying offline
   TankControllers kills the scene player tank → `CameraFollow.target` goes null → static camera for every human
   client. Fix: in `NetworkTank.OnStartClient`, when `IsOwner`, `FindFirstObjectByType<CameraFollow>()` and set
   `target = transform`. The rest is fine: TankNet is built by unpacking Tank.prefab (PrefabFactory.CreateTankNetPrefab),
   so the owner's spawned tank KEEPS DevHud and PlaytestHotkeys (OnStartClient destroys them on non-owners only) —
   HUD and weapon keys survive on the owned tank; scene-jump F-keys in networked play are now a desync hazard but
   inert until pressed (note it, don't fix in M3). Disabling FfaGameMode also silences its OnGUI scoreboard — fine.
   Do the cleanup inside `StartServer/StartClient/StartHost` so the H/J dev hotkeys get it too.
4. **Builder API: confirmed shape.** Unity 6: `BuildPlayerOptions.subtarget` is a public **int** — set
   `subtarget = (int)StandaloneBuildSubtarget.Server`, and ALSO set
   `EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Server` before the build so script
   compilation (`UNITY_SERVER` define) matches the player build. `locationPathName` = `<out>/potshot-server`
   (yields ELF + `potshot-server_Data/` + `UnityPlayer.so`). Builder must parse `-potshotOut` from
   `Environment.GetCommandLineArgs` (BuildMacDev hardcodes its path today). `-potshotVersion` is currently consumed
   by nothing — leave `GameVersion.cs` at its constant (client+server both say `0.1.0+dev`, handshake passes) and
   tag the image with the git sha; either drop the arg or just log it, and say in the task doc that it's decorative.
   Accept that the version handshake does NOT yet distinguish stale builds — real stamping is a later milestone.
5. **BUG — `scripts/build-server.sh --docker` builds the wrong arch.** Line 19 is plain `docker build` with no
   `--platform`; on this arm64 Mac it produces an arm64 image that will CrashLoop on the amd64 Linode nodes. Fix:
   `docker buildx build --platform linux/amd64 --push` (or `docker build --platform linux/amd64` + separate push).
   No compile happens in the container, so emulation cost is nil.
6. **Dockerfile: ubuntu:24.04 is the safe pick.** It ships libstdc++6/libgcc which Mono Unity players need;
   debian:bookworm-slim also has libstdc++6 (apt depends on it) but lacks ca-certificates — if chosen, add it.
   `-logFile /dev/stdout` is correct on Linux players. `-batchmode -nographics` are redundant for a Server-subtarget
   build but harmless — keep them. Non-root USER is a nice-to-have, not required for M3.
7. **K8s specifics: mostly right, three requirements.** (a) `potshot.kodloki.io/game-node` is a valid label key
   (DNS-subdomain prefix + short name). (b) `imagePullPolicy: Always` is REQUIRED on the `:dev` tag or Recreate
   redeploys silently reuse the cached image. (c) Skip liveness/readiness probes (UDP is not probeable; an exec
   `pgrep` liveness adds little for a walking skeleton) and set `terminationGracePeriodSeconds: 30` explicitly.
   hostPort+Recreate single-replica is correct. Record WHICH node gets the label in the task doc, since step 5
   hardcodes its IP until the M4 reconciler exists.
8. **E2E: define the observable success line, and add it first.** `-batchmode` on the Mac player build is
   supported (null display). But today NO log proves success: VersionAuthenticator logs only on rejection
   client-side, and NetworkTank spawns silently. Add in step 1's change: (a) `Debug.Log("[Net] authenticated by
   server")` in `OnClientReceivedResponse` when `msg.Passed`; (b) `Debug.Log("[Net] owned tank spawned")` in
   `NetworkTank.OnStartClient` when `IsOwner`. Grep the client `-logFile` for the owned-tank line — it proves
   handshake AND spawn in one string. The Mac client must be REBUILT after the step-1/step-8 code changes; the
   existing PotshotDev.app predates them.
9. **Direct kubectl apply vs ArgoCD:** fine for M3 (no ArgoCD Application exists for potshot yet), but committing
   `K8s/` while deferring ArgoCD means M4 must adopt these exact manifests — keep them declarative-clean (no
   `kubectl create` imperatives, namespace in its own file as planned).

## Required changes

(1) split game/status hostnames + doc fix; (2) DNS create before E2E, verify via authoritative NS, first test by
node IP; (3) camera re-target on owned spawn; (4) set both subtarget fields, parse `-potshotOut`; (5) add
`--platform linux/amd64` to the docker path; (7b) `imagePullPolicy: Always`; (8) success-log lines + client rebuild.
