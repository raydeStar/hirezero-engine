#!/bin/sh
# Builds the pinned browser image with no network and qualifies it on this VM's own engine: the real-image
# test runs fictional pages through the actual gVisor browser and confirms its resources are released.
# Run as root after bootstrap.sh, from a checkout of this repository. It never calls a model.
set -eu

ROOT=/opt/hirezero-qualification
REPO=$(cd "$(dirname "$0")/../.." && pwd)
cd "$REPO"
fail() { printf '%s\n' "qualify: $*" >&2; exit 1; }
[ -s "$ROOT/receipts/bootstrap.json" ] || fail "run deploy/vultr/bootstrap.sh first"
[ "$(df --output=avail -B1G / | tail -1 | tr -d ' ')" -ge 10 ] || fail "needs at least 10 GiB free"

export DOTNET_ROOT="$ROOT/dotnet" DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
export PATH="$DOTNET_ROOT:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin"
LOGS="$ROOT/receipts"

docker build --pull=false --network=none --platform=linux/amd64 \
  -f runtime/browser/Dockerfile -t hirezero-browser:candidate runtime/browser >"$LOGS/browser-build.log" 2>&1 ||
  fail "browser image build failed; see $LOGS/browser-build.log"
IMAGE=$(docker image inspect --format '{{.Id}}' hirezero-browser:candidate)

dotnet restore tests/HireZero.Engine.Tests --locked-mode >"$LOGS/restore.log" 2>&1 || fail "restore failed; see $LOGS/restore.log"
dotnet build tests/HireZero.Engine.Tests --no-restore >"$LOGS/test-build.log" 2>&1 || fail "build failed; see $LOGS/test-build.log"

RECEIPT="$LOGS/browser-qualification-$(date -u +%Y%m%dT%H%M%SZ).json"
export HIREZERO_BROWSER_TEST_IMAGE="$IMAGE"
export HIREZERO_BROWSER_TEST_DOCKER=/usr/bin/docker
export HIREZERO_BROWSER_TEST_ENDPOINT=unix:///var/run/docker.sock
HIREZERO_BROWSER_TEST_INSTALLATION=$(cat "$ROOT/receipts/installation-id")
export HIREZERO_BROWSER_TEST_INSTALLATION
export HIREZERO_BROWSER_TEST_RECEIPT="$RECEIPT"

# A skip is not a pass: the run must report the real-image test as succeeded.
dotnet test --project tests/HireZero.Engine.Tests --no-build -- --filter-class BrowserRuntimeTests \
  >"$LOGS/browser-qualification.log" 2>&1 || fail "qualification failed; see $LOGS/browser-qualification.log"
grep -q "skipped: 0" "$LOGS/browser-qualification.log" || fail "the real-image test was skipped, so nothing was qualified"
[ -s "$RECEIPT" ] || fail "no qualification receipt was written"
docker ps -aq --filter label=io.hirezero.browser.installation | grep -q . && fail "a browser container was left behind"

ln -sf "$RECEIPT" "$LOGS/browser-qualification.json" # the path tools/HireZero.Engine.Run reads by default
printf 'qualify: browser image %s qualified on this VM; receipt %s\n' "$IMAGE" "$RECEIPT"
