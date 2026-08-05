# M3 — Linux server build, Docker, first cluster deploy

## Author: fable-primary
## Status: Not Started

Brandon (2026-08-04): deploy to the kodloki cluster for testing now.
Walking-skeleton deploy: movement-only multiplayer on real infra; m2c
(networked weapons) continues against a live server afterward.

## Plan

1. **Offline-actor cleanup** (prereq): when a networked session starts
   (NetBootstrap StartServer/StartClient/StartHost), destroy scene-local
   offline actors — TankControllers WITHOUT NetworkObject, and disable
   FfaGameMode (it would double-manage). Otherwise every client runs
   phantom local bots + a second input-claiming scene tank alongside the
   networked tanks. Networked FFA/bots return in m2c.
2. **Builder.BuildLinuxServer**: BuildTarget.StandaloneLinux64 +
   StandaloneBuildSubtarget.Server (module installed at M0), Mono
   backend (no Linux IL2CPP toolchain on this Mac), scenes = DevArena +
   maps (same list as client), output `server/build/potshot-server`
   via `-potshotOut`/`-potshotVersion` args (scripts/build-server.sh
   already passes them). Server autostarts via existing `-potshotServer`
   CLI arg (NetBootstrap); scene 0 (DevArena) loads by default.
3. **Docker**: `server/Dockerfile` — slim Debian/Ubuntu base, copy build,
   run binary with `-potshotServer -batchmode -nographics -logFile
   /dev/stdout`; expose 7777/udp. Build for linux/amd64 (Linode nodes)
   via buildx from the arm64 Mac. Tag `bholcombe/potshot-server:<sha>`
   + `:dev`, push with dockerhub_pat (never echo). Verify locally first:
   run the container on the Mac, connect a headless client to
   localhost, assert authentication in logs.
4. **Cluster deploy (test-grade M4-lite)**: `K8s/namespace.yaml` +
   `K8s/deployment-server.yaml` (single replica, Recreate, hostPort
   7777/udp, nodeSelector `potshot.kodloki.io/game-node=true`, resources
   250m/512Mi, image :dev). Label one tow-c1 node; kubectl apply
   (kubeconfig ~/.kube/linode-config). ArgoCD wiring + DNS CronJob +
   status endpoint stay in M4 proper.
5. **DNS**: one-shot set `potshot.kodloki.io` A record → labeled node's
   public IP via Linode API (linode_pat). The reconciler CronJob is M4.
6. **E2E verification (agent-run)**: headless client from the Mac —
   `PotshotDev.app/Contents/MacOS/potshot -batchmode -nographics
   -potshotClient potshot.kodloki.io -logFile <path>` — assert log shows
   connection + authentication + owned tank spawn. That proves: Linux
   build, container, UDP through the 1:1 NAT, handshake, spawn.
7. Docs: deployment.md updated with actuals; PROJECT_STATUS M3 row.

## Risks

- Mono cross-build from macOS to Linux server target: supported, but
  first build may surface missing-module errors — module was installed
  headlessly at M0, verify Builder reports Succeeded.
- buildx amd64 emulation for the docker build is slow but fine (no
  compile inside the container — just file copy).
- hostPort + 1:1 NAT (per tow-c1 memory): server binds 0.0.0.0, clients
  hit the node public IP; no external-ip advertisement issue for
  client-initiated UDP (unlike TURN).
- Unity Mono server builds need libc/ssl basics — pick base image
  accordingly (ubuntu:24.04 or debian:bookworm-slim + ca-certificates).
