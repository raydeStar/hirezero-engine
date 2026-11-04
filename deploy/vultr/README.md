# Running the engine on a Vultr VM

The VM is disposable. Create it for a working session and destroy it at the end. A Vultr VM bills by the hour
until it is destroyed, whether or not it is running, so these scripts have no "stop".

| Plan (default) | Size | Price |
| --- | --- | --- |
| `vc2-2c-4gb` | 2 vCPU, 4 GB RAM, 80 GB disk, Ubuntu 24.04 | $0.027/hour, at most $20/month |

Override the plan with `HIREZERO_VULTR_PLAN` and the branch the VM clones with `HIREZERO_ENGINE_REF`.

## One session

On your machine, with your Vultr **account** API key (not the inference key) in `VULTR_API_KEY`:

```sh
sh deploy/vultr/vm.sh plan                                     # what it would create and cost; sends nothing
sh deploy/vultr/vm.sh ssh-keys                                 # pick the SSH key to log in with
sh deploy/vultr/vm.sh create --region dfw --ssh-key <id> --yes # billing starts here
sh deploy/vultr/vm.sh list                                     # its id and IP address
```

On first boot, cloud-init clones this repository to `/opt/hirezero-engine` and runs
`bootstrap.sh --verify-only`. That run downloads every pinned host file, checks its size and hash, and simulates
the install. It installs nothing. Its log is `/var/log/hirezero-bootstrap.log`.

On the VM, as root:

```sh
cd /opt/hirezero-engine
sh deploy/vultr/bootstrap.sh      # Docker, gVisor and .NET at their pinned versions; SSH-only firewall
sh deploy/vultr/qualify.sh        # build the browser image with no network, qualify it on this engine
export VULTR_INFERENCE_API_KEY=... # typed in the shell, never written to a file
DOTNET_ROOT=/opt/hirezero-qualification/dotnet PATH=/opt/hirezero-qualification/dotnet:$PATH \
  dotnet run --project tools/HireZero.Engine.Smoke -- --live
DOTNET_ROOT=/opt/hirezero-qualification/dotnet PATH=/opt/hirezero-qualification/dotnet:$PATH \
  dotnet run --project tools/HireZero.Engine.Run -- --goal "What does this site offer?" --site https://example.com
```

Copy back anything worth keeping (`runs/`, `receipts/`), then on your machine:

```sh
sh deploy/vultr/vm.sh destroy <id> --yes   # billing stops; the disk is deleted
```

## What each script guarantees

- **`vm.sh`** creates at most one VM, tagged `hirezero-engine`, with no backups. It will only destroy instances
  carrying that tag, and every billable action needs `--yes`.
- **`bootstrap.sh`** refuses anything but a fresh Ubuntu 24.04 amd64 machine with at least 30 GiB free. Every
  download must match `runtime/qualification-host/pin.json` before anything is installed. Ubuntu packages already
  present at the pinned version or newer are kept rather than downgraded. The Docker daemon settings go in before
  the packages, so Docker never creates a bridge network or firewall rules. The engine must report runsc, cgroup
  v2 with systemd, and working memory, swap, process and CPU limits.
- **`qualify.sh`** builds the browser image with `--network=none` and runs the real-image test. A skipped test
  counts as a failure, and so does a browser container left behind.

## Checked so far

These checks ran before any VM exists, on Nov 3:

- all three scripts pass `sh -n`;
- `vm.sh plan` reads live prices;
- `vm.sh` refuses to create without `--yes`, and refuses anything without a key;
- every pinned download URL is live at its pinned size.

The scripts have not yet run on a VM, so bootstrap's package simulation on Vultr's image is still unproven.
