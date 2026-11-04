#!/bin/sh
# Creates, lists and destroys the one disposable VM the engine runs on. Uses the Vultr API with your account
# key in VULTR_API_KEY (an account key, not the inference key). A VM bills by the hour until it is destroyed,
# stopped or not, so this script has no "stop": destroy it at the end of every session.
#
#   sh deploy/vultr/vm.sh plan                                # what create would make and what it costs; sends nothing
#   sh deploy/vultr/vm.sh create --region dfw --ssh-key <id> --yes
#   sh deploy/vultr/vm.sh list                                # VMs tagged hirezero-engine
#   sh deploy/vultr/vm.sh destroy <instance id> --yes
#   sh deploy/vultr/vm.sh ssh-keys                            # ids of the SSH keys on your account
#
# Only instances carrying the hirezero-engine tag can be destroyed through this script.
set -eu

API=https://api.vultr.com/v2
TAG=hirezero-engine
PLAN=${HIREZERO_VULTR_PLAN:-vc2-2c-4gb}
OS_ID=2284 # Ubuntu 24.04 LTS x64
REPO_URL=https://github.com/raydeStar/hirezero-engine.git
REF=${HIREZERO_ENGINE_REF:-vultr-agent-rush}

fail() { printf '%s\n' "vm: $*" >&2; exit 1; }
json() { python3 -c "import json,sys; d=json.load(sys.stdin); $1"; }
call() {
  [ -n "${VULTR_API_KEY:-}" ] || fail "set VULTR_API_KEY (Vultr account > API) in your shell"
  method=$1; path=$2; shift 2
  curl -sS --fail-with-body --max-time 60 -X "$method" -H "Authorization: Bearer $VULTR_API_KEY" \
    -H "Content-Type: application/json" "$@" "$API$path"
}

price() {
  curl -sS --max-time 30 "$API/plans?per_page=500" |
    json "p=[p for p in d['plans'] if p['id']=='$PLAN']; p=p[0] if p else None; print(f\"{p['id']}: {p['vcpu_count']} vCPU, {p['ram']//1024} GB RAM, {p['disk']} GB disk, \${p['hourly_cost']}/hour, at most \${p['monthly_cost']}/month\" if p else 'unknown plan')"
}

user_data() {
  # cloud-init: clone the public repository at the chosen branch and verify the pinned host stack. It only
  # verifies; installing is a separate, deliberate step over SSH.
  cat <<EOF | base64 | tr -d '\n'
#cloud-config
runcmd:
  - [sh, -c, "command -v git >/dev/null || (apt-get update -q && apt-get install -yq git)"]
  - [git, clone, --depth, "1", --branch, "$REF", "$REPO_URL", /opt/hirezero-engine]
  - [sh, -c, "sh /opt/hirezero-engine/deploy/vultr/bootstrap.sh --verify-only >/var/log/hirezero-bootstrap.log 2>&1"]
EOF
}

arg() { # the value after a flag, if given
  flag=$1; shift
  while [ $# -gt 0 ]; do [ "$1" = "$flag" ] && [ $# -gt 1 ] && { printf '%s' "$2"; return; }; shift; done
}
has() { flag=$1; shift; for a in "$@"; do [ "$a" = "$flag" ] && return 0; done; return 1; }

command=${1:-plan}
[ $# -gt 0 ] && shift
case "$command" in plan) ;; *) [ -n "${VULTR_API_KEY:-}" ] || fail "set VULTR_API_KEY (Vultr account > API) in your shell" ;; esac
case "$command" in
  plan)
    printf 'vm: would create %s\n' "$(price)"
    printf 'vm: Ubuntu 24.04, tag %s, no backups, cloning %s at %s\n' "$TAG" "$REPO_URL" "$REF"
    ;;
  ssh-keys)
    call GET /ssh-keys | json "[print(k['id'], k['name']) for k in d['ssh_keys']] or print('vm: no SSH keys on this account; add one in the Vultr console first')"
    ;;
  list)
    call GET "/instances?tag=$TAG" | json "[print(i['id'], i['label'], i['region'], i['plan'], i['main_ip'], i['status'], i['date_created']) for i in d['instances']] or print('vm: none running')"
    ;;
  create)
    region=$(arg --region "$@"); key=$(arg --ssh-key "$@")
    [ -n "$region" ] || fail "choose a region, for example --region dfw (Dallas) or --region sea (Seattle)"
    [ -n "$key" ] || fail "choose an SSH key with --ssh-key <id> (see: vm.sh ssh-keys)"
    printf 'vm: %s in %s\n' "$(price)" "$region"
    has --yes "$@" || fail "this starts hourly billing; add --yes to create it"
    existing=$(call GET "/instances?tag=$TAG" | json "print(len(d['instances']))")
    [ "$existing" = 0 ] || fail "a $TAG VM already exists; destroy it first (vm.sh list)"
    body=$(python3 -c 'import json,sys; print(json.dumps({"region": sys.argv[1], "plan": sys.argv[2], "os_id": int(sys.argv[3]), "label": "hirezero-engine", "tags": [sys.argv[4]], "sshkey_id": [sys.argv[5]], "backups": "disabled", "enable_ipv6": False, "user_data": sys.argv[6]}))' \
      "$region" "$PLAN" "$OS_ID" "$TAG" "$key" "$(user_data)")
    call POST /instances --data "$body" | json "i=d['instance']; print('vm: created', i['id'], 'in', i['region'], '- billing started; destroy it with: vm.sh destroy', i['id'], '--yes')"
    ;;
  destroy)
    id=${1:-}
    [ -n "$id" ] || fail "name the instance: vm.sh destroy <id> --yes (see: vm.sh list)"
    tags=$(call GET "/instances/$id" | json "print(','.join(d['instance'].get('tags', [])))")
    case ",$tags," in *",$TAG,"*) ;; *) fail "instance $id is not tagged $TAG; refusing to destroy it" ;; esac
    has --yes "$@" || fail "this permanently deletes $id and its disk; add --yes"
    call DELETE "/instances/$id" >/dev/null
    printf 'vm: destroyed %s; billing for it has stopped\n' "$id"
    ;;
  *) fail "unknown command $command (plan, ssh-keys, list, create, destroy)" ;;
esac
