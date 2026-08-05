# Deployment

Target: **tow-c1** (Linode LKE, us-sea-2), kubeconfig `~/.kube/linode-config`.
Namespace `potshot`. Manifests in `K8s/` (shine layout), ArgoCD watches the
GitHub remote.

## Game traffic (UDP) — the non-standard part

Standard LKE NodeBalancers **do not support UDP** (Premium is LKE-E only), so
the game server cannot sit behind a `LoadBalancer` service:

- Pod runs with `hostPort: 7777` (protocol UDP) — or hostNetwork if hostPort
  misbehaves — pinned via nodeSelector to a labeled node
  (`potshot.kodloki.io/game-node=true`).
- Clients connect to `potshot.kodloki.io:7777` → DNS A-record → that node's
  **public IP**. Nodes are 1:1-NATed (VPC 172.31.x internally); inbound UDP
  to the public IP reaches the pod fine. The server binds 0.0.0.0 and never
  advertises its own IP, so the NAT asymmetry that bites TURN does not apply.
- **DNS reconciler CronJob** (every 5 min): compares the labeled node's public
  IP to the `potshot.kodloki.io` A-record via Linode API (`linode_pat` from
  the eloup-wizard secrets, mounted as a Secret); updates on drift. Node
  recycles therefore cost ≤5 min + client reconnect.

## Status endpoint (HTTP) — SPLIT HOST (M3 review)

One hostname cannot point at both the game node and the ingress LB.
Game: `potshot.kodloki.io` → game node public IP (A record, TTL 300).
Status (M4): `status.potshot.kodloki.io` → 172.232.176.47 via ingress-nginx
+ cert-manager. `GET /status` (players online, version, uptime, map) on
8080/tcp; serves the client download link at M5.

## Deployed state (M3, 2026-08-04)

- Live: `bholcombe/potshot-server:dev` (+ git-sha tag) on tow-c1, namespace
  `potshot`, node `lke484433-700897-418c61570000` (label
  `potshot.kodloki.io/game-node=true`), hostPort 7777/udp.
  `potshot.kodloki.io` → 172.238.48.241. E2E verified: headless Mac client
  authenticated + spawned across the internet.
- Node internal IPs are 192.168.x (docs previously said 172.31.x — corrected).
- Known limitation (fix in M4): the labeled node is an AUTOSCALED node —
  scale-down or recycle strands the pod (only one node is labeled) and
  changes the public IP. The M4 DNS-reconciler CronJob + a labeled
  stable-pool node address this. kubectl applied by hand; ArgoCD wiring
  is also M4.

## Images

- `bholcombe/potshot-server:<git-sha>` — headless Linux server.
  Built by `scripts/build-server.sh` + `server/Dockerfile`; pushed with
  `dockerhub_pat`. `:dev` tag for the rolling dev build.

## Resources

10-player headless Unity server: request `250m` CPU / `512Mi`, limit `1` CPU /
`1Gi`. Always-on; restart policy Always; single replica (`Recreate` strategy —
two servers on one hostPort cannot coexist).
