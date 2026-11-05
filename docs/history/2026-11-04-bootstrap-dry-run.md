# Bootstrap dry run on Ubuntu 24.04

On Nov 4, 2026, a disposable Linux/amd64 container on Docker Desktop ran the
[bootstrap script](../../deploy/vultr/bootstrap.sh) from commit `180aa21`. The Ubuntu
24.04 image digest was `sha256:fe81560990fae86dc4743202b2cb4d3df98d0bcf949173cecef15ce1127ef852`.
The checkout was mounted read-only. The only requested prerequisite packages were
`python3`, `curl` and `ca-certificates`, installed with `--no-install-recommends`.

```sh
HIREZERO_HOST_ROOT=/tmp/hz sh deploy/vultr/bootstrap.sh --verify-only
```

The script exited 0 after checking the sizes and hashes of all 15 non-WSL
[host artifacts](../../runtime/qualification-host/pin.json). Its final output was:

```text
bootstrap: every download matches its pin
bootstrap: package simulation passed (13 packages)
bootstrap: verify-only: all pins match and the install simulates cleanly; nothing was installed
```

Docker removed the container and its downloads on exit. A container-list check
confirmed removal, and the newly pulled Ubuntu image was removed. The Windows
volume had 222 GiB free before startup, 218 GiB during the downloads and 216 GiB
after cleanup. Installation, service startup and qualification on a Vultr VM
still need their own run.
