#!/bin/bash
# STRICT gate → deploy pipeline. Any failure at any step aborts loudly.
# Two false-green deploys happened via ad-hoc chains (swallowed compile
# errors + stale test XMLs) — this script is the only sanctioned path.
# Usage: scripts/deploy.sh [--client-only]
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$REPO_ROOT"

echo "== 1/5 fresh test results (stale XMLs caused a false-green gate)"
rm -f game/Logs/test-results/EditMode.xml game/Logs/test-results/PlayMode.xml

echo "== 2/5 gate"
scripts/run-tests.sh # exits nonzero on any platform failure
python3 - <<'EOF'
import xml.etree.ElementTree as ET, sys, os
bad = 0
for p in ['EditMode', 'PlayMode']:
    path = f'game/Logs/test-results/{p}.xml'
    if not os.path.exists(path):
        print(f'{p}: NO RESULTS FILE — run did not complete')
        sys.exit(1)
    r = ET.parse(path).getroot()
    print(p, r.get('passed'), 'passed,', r.get('failed'), 'failed')
    for tc in r.iter('test-case'):
        if tc.get('result') != 'Passed':
            bad += 1
            m = tc.find('.//message')
            print(' FAIL:', tc.get('name'), '—',
                  ((m.text or '').strip()[:200] if m is not None else ''))
if bad:
    print('GATE RED — ABORTING DEPLOY')
    sys.exit(1)
EOF

SHA=$(git rev-parse --short HEAD)

if [ "${1:-}" != "--client-only" ]; then
    echo "== 3/5 server image ($SHA)"
    scripts/build-server.sh --docker
    docker image inspect "bholcombe/potshot-server:$SHA" > /dev/null # tag must exist
    DOCKERHUB_PAT=$(python3 -c "import json; print(json.load(open('$HOME/.config/eloup-wizard/secrets.json'))['dockerhub_pat'])")
    echo "$DOCKERHUB_PAT" | docker login -u bholcombe --password-stdin > /dev/null
    docker push -q "bholcombe/potshot-server:$SHA"
    docker push -q bholcombe/potshot-server:dev

    echo "== 4/5 rollout"
    export KUBECONFIG=~/.kube/linode-config
    kubectl -n potshot rollout restart deployment/potshot-server
    kubectl -n potshot rollout status deployment/potshot-server --timeout=180s
else
    echo "== 3-4/5 skipped (--client-only)"
fi

echo "== 5/5 client build"
scripts/unity.sh -quit -executeMethod Potshot.EditorTools.Builder.BuildMacDev 2>&1 \
    | grep "\[Builder\] Succeeded" # grep fails the script if the build failed

echo "DEPLOY OK ($SHA)"
