# K8s manifests (M4)

Shine layout, namespace `potshot`, ArgoCD watches the GitHub remote. Planned
manifests (see `docs/deployment.md` for the why):

- `namespace.yaml`
- `deployment-server.yaml` — single replica, `Recreate`, `hostPort 7777/udp`,
  nodeSelector `potshot.kodloki.io/game-node=true`, image
  `bholcombe/potshot-server`
- `service-status.yaml` + `ingress.yaml` — HTTP status on
  `potshot.kodloki.io` via ingress-nginx + cert-manager `letsencrypt-prod`
- `cronjob-dns-sync.yaml` + `secret-linode.template.yaml` — A-record
  reconciler (labeled node public IP → `potshot.kodloki.io`)
