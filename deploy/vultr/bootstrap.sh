#!/bin/sh
# Prepares a fresh Vultr Ubuntu 24.04 amd64 VM to run the contained browser: Docker, gVisor (runsc) and the
# .NET SDK, all at the exact versions, sizes and hashes in runtime/qualification-host/pin.json. Nothing is
# installed unless every download matches its pin first. Run as root from a checkout of this repository.
#
#   sh deploy/vultr/bootstrap.sh --verify-only   # download and verify everything, simulate the installs, change nothing
#   sh deploy/vultr/bootstrap.sh                 # the same checks, then install
#
# The VM is disposable: it is destroyed after each working session, never stopped and reused, so this script
# refuses a machine that already has a Docker engine instead of trying to repair one.
set -eu

ROOT=/opt/hirezero-qualification
REPO=$(cd "$(dirname "$0")/../.." && pwd)
PIN="$REPO/runtime/qualification-host/pin.json"
BROWSER_PIN="$REPO/runtime/browser/pin.json"
MODE=install
[ "${1:-}" = "--verify-only" ] && MODE=verify
# A verify-only run may use another folder (for a local dry run); the install uses the path daemon.json names.
[ "$MODE" = verify ] && ROOT=${HIREZERO_HOST_ROOT:-$ROOT}

say() { printf '%s\n' "bootstrap: $*"; }
fail() { printf '%s\n' "bootstrap: $*" >&2; exit 1; }

# --- The machine is what the pins were written for ---------------------------------------------------------
[ "$(id -u)" = 0 ] || fail "run as root"
. /etc/os-release
[ "$ID" = ubuntu ] && [ "$VERSION_ID" = "24.04" ] || fail "needs Ubuntu 24.04, found $ID $VERSION_ID"
[ "$(dpkg --print-architecture)" = amd64 ] || fail "needs amd64"
command -v python3 >/dev/null || fail "needs python3 (present on Vultr's Ubuntu image)"
command -v curl >/dev/null || fail "needs curl"
free_gib() { df --output=avail -B1G "$1" | tail -1 | tr -d ' '; }
[ "$(free_gib /)" -ge 30 ] || fail "needs at least 30 GiB free on / (have $(free_gib /) GiB)"
if [ "$MODE" = install ]; then
  [ ! -e /etc/docker/daemon.json ] || fail "/etc/docker/daemon.json already exists; use a fresh VM"
  ! command -v dockerd >/dev/null || fail "a Docker engine is already installed; use a fresh VM"
fi

# --- Download and verify every pinned file before anything is installed -----------------------------------
install -d -m 0700 "$ROOT/downloads" "$ROOT/receipts"
python3 - "$PIN" "$ROOT/downloads" <<'PY'
import hashlib, json, os, sys, urllib.request
pin, downloads = sys.argv[1], sys.argv[2]
for a in json.load(open(pin))["artifacts"]:
    if a["kind"] == "wsl":
        continue  # the WSL image is only for the local qualification host
    path = os.path.join(downloads, a["name"])
    if not os.path.exists(path):
        print("bootstrap: downloading", a["name"], flush=True)
        with urllib.request.urlopen(a["url"], timeout=120) as r, open(path + ".part", "wb") as f:
            while chunk := r.read(1 << 20):
                f.write(chunk)
        os.replace(path + ".part", path)
    size = os.path.getsize(path)
    h = hashlib.new(a["algorithm"])
    with open(path, "rb") as f:
        while chunk := f.read(1 << 20):
            h.update(chunk)
    if size != a["bytes"] or h.hexdigest().lower() != a["hash"].lower():
        os.remove(path)
        sys.exit(f"bootstrap: {a['name']} does not match its pin ({size} bytes); removed it, nothing installed")
print("bootstrap: every download matches its pin", flush=True)
PY
[ "$(free_gib /)" -ge 10 ] || fail "free space fell below 10 GiB after downloads"

# Ubuntu packages already present at the pinned version or newer are left alone: installing an older pinned
# copy would be a downgrade. Docker's packages are always the pinned ones.
DEBS=$(python3 - "$PIN" "$ROOT/downloads" <<'PY'
import json, subprocess, sys
pin, downloads = sys.argv[1], sys.argv[2]
for a in json.load(open(pin))["artifacts"]:
    if a["kind"] != "deb":
        continue
    installed = subprocess.run(["dpkg-query", "-W", "-f=${Version}", a["package"]], capture_output=True, text=True)
    if (a["publisher"] == "Ubuntu" and installed.returncode == 0 and installed.stdout
            and subprocess.run(["dpkg", "--compare-versions", installed.stdout, "ge", a["version"]]).returncode == 0):
        print(f"bootstrap: keeping {a['package']} {installed.stdout} (pin {a['version']})", file=sys.stderr)
        continue
    print(f"{downloads}/{a['name']}")
PY
)
# shellcheck disable=SC2086 # DEBS is a newline-separated list of paths without spaces
dpkg --simulate --install $DEBS >"$ROOT/receipts/dpkg-simulate.log" 2>&1 || {
  cat "$ROOT/receipts/dpkg-simulate.log" >&2
  fail "the pinned packages do not install cleanly on this image; nothing was installed"
}
say "package simulation passed ($(printf '%s\n' "$DEBS" | wc -l) packages)"

if [ "$MODE" = verify ]; then
  say "verify-only: all pins match and the install simulates cleanly; nothing was installed"
  exit 0
fi

# --- Install: gVisor and .NET into their own folders, then the engine with its reviewed daemon settings ------
install -d -m 0755 "$ROOT" "$ROOT/gvisor" "$ROOT/dotnet"
python3 -m tarfile -e "$ROOT/downloads/gvisor-x86_64.tar.bz2" "$ROOT/gvisor"
python3 -m tarfile -e "$ROOT/downloads/dotnet-sdk-10.0.403-linux-x64.tar.gz" "$ROOT/dotnet"
test -x "$ROOT/gvisor/runsc" && test -d "$ROOT/gvisor/gvisor-bin" || fail "the gVisor bundle is not laid out as pinned"
chown -R root:root "$ROOT/gvisor" && chmod -R go-w "$ROOT/gvisor"

# The daemon settings go in before the packages, so Docker's first start never creates a bridge or firewall rules.
install -d -m 0755 /etc/docker
install -m 0644 "$REPO/runtime/qualification-host/daemon.json" /etc/docker/daemon.json
# shellcheck disable=SC2086
dpkg --install $DEBS >"$ROOT/receipts/dpkg-install.log" 2>&1 || {
  cat "$ROOT/receipts/dpkg-install.log" >&2
  fail "package install failed; logs kept in $ROOT/receipts. Destroy this VM rather than repairing it."
}
apt-mark hold docker-ce docker-ce-cli containerd.io docker-buildx-plugin >/dev/null
systemctl restart docker

# The engine must be what the browser adapter will check for: runsc registered, cgroup v2 with systemd, and
# memory, swap, process and CPU limits all supported.
docker info --format '{{json .}}' | python3 -c '
import json, sys
i = json.load(sys.stdin)
need = {"runsc registered": "runsc" in i.get("Runtimes", {}), "cgroup v2": i.get("CgroupVersion") == "2",
        "systemd cgroups": i.get("CgroupDriver") == "systemd", "memory limit": i.get("MemoryLimit"),
        "swap limit": i.get("SwapLimit"), "pids limit": i.get("PidsLimit"), "cpu quota": i.get("CpuCfsQuota")}
missing = [k for k, ok in need.items() if not ok]
sys.exit("bootstrap: engine is missing " + ", ".join(missing) if missing else 0)'

# The browser base image, by digest, so the image build itself can run with no network.
BASE=$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["baseImage"])' "$BROWSER_PIN")
docker pull --platform linux/amd64 "$BASE" >/dev/null
[ "$(free_gib /)" -ge 10 ] || fail "free space fell below 10 GiB after pulling the browser base"

# A firewall that admits only SSH. Outbound stays open for the host broker and Vultr inference; the containers
# themselves have no network at all.
if command -v ufw >/dev/null; then
  ufw --force reset >/dev/null
  ufw default deny incoming >/dev/null
  ufw default allow outgoing >/dev/null
  ufw allow 22/tcp >/dev/null
  ufw --force enable >/dev/null
fi

[ -e "$ROOT/receipts/installation-id" ] || python3 -c 'import uuid; print(uuid.uuid4().hex)' >"$ROOT/receipts/installation-id"
{
  printf '{"at":"%s","runsc":"%s","docker":"%s","dotnet":"%s","browserBase":"%s"}\n' \
    "$(date -u +%Y-%m-%dT%H:%M:%SZ)" \
    "$("$ROOT/gvisor/runsc" --version | head -1)" \
    "$(docker version --format '{{.Server.Version}}')" \
    "$("$ROOT/dotnet/dotnet" --version)" \
    "$BASE"
} >"$ROOT/receipts/bootstrap.json"
say "ready: $(cat "$ROOT/receipts/bootstrap.json")"
say "next: sh deploy/vultr/qualify.sh"
