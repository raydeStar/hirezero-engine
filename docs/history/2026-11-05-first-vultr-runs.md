# First runs on the Vultr VM · November 5, 2026

One `vc2-2c-4gb` VM in Los Angeles (`lax`), Ubuntu 24.04, created with `deploy/vultr/vm.sh`. Every model call
below went to Vultr Serverless Inference (`deepseek-v4.1-flash-1017`).

| Step | Result |
| --- | --- |
| First-boot `bootstrap.sh --verify-only` | Every download matched its pin; the package simulation passed; nothing installed. |
| `bootstrap.sh` | runsc release-20261019.0, Docker 29.9.2, .NET 10.0.403, browser base pulled by digest; ufw allows SSH only. |
| `qualify.sh` | Browser image `sha256:4a88988656a3…` qualified on this VM; no container left behind. |
| Smoke call from the VM | `pong` in 0.8 s, 45 input and 4 output tokens. |

## The browser task

Goal: "Which license does the page suggest for someone who wants it simple and permissive?" on
`https://choosealicense.com`, approved at the prompt by its digest.

**First attempt (failed, contained).** The model drafted `wait-for main`; the page has no `<main>`, so the
browser waited out after 21 s and captured nothing. Containment held: 5 requests allowed, the favicon refused
as out of scope, `fonts.googleapis.com` refused as off-site, teardown confirmed, 783 tokens. The draft rules now
default to `body` and the approval prompt names the exact code to type (commit `0a442ab`). The failed job was not
rerun; the second attempt is a new draft with a new approval.

**Second attempt (completed).**

| Evidence | Value |
| --- | --- |
| Itinerary | navigate `https://choosealicense.com/`, wait-for `body`, read `body` |
| Browser | completed in 11.4 s; one capture of 1,333 characters |
| Host broker | 5 allowed; `GET /favicon.ico` refused as outside the public-page scope |
| Browser-reported off-site refusal | `https://fonts.googleapis.com` |
| Teardown | confirmed (`released`); `docker ps -a` empty afterwards |
| Model calls | draft 225 in / 243 out (202 reasoning), 1.2 s; answer 482 in / 130 out (65 reasoning), 1.6 s |
| Tokens | 1,080 this run |

Answer: the page suggests the MIT License, which it describes as short and permissive, allowing closed-source
redistribution.

The full receipts stay on the VM under `runs/` and `receipts/`; only this summary is in the repository.

## Public web desk

`https://rush.hirezero.app` points at the VM. The web app was published to `/srv/hirezero/app` and runs as two
systemd services under separate system accounts, both bound to loopback: the read-only gallery (`hz-gallery`, port
5078, no Docker, key or passphrase) and the owner worker (`hz-worker`, port 5077, in the `docker` group, reading the
qualification receipt, the Vultr key and the owner passphrase from root-only environment files). Nginx terminates
TLS with a Let's Encrypt certificate (valid to February 3, 2027) and adds HSTS. Port 80 was open only while the
certificate was issued; ufw now allows 22 and 443.

Checked from outside the VM: the page, gallery and session endpoints answer 200; the private job list answers 401;
a gallery POST answers 403; ports 80, 5077 and 5078 refuse connections. The gallery shows one reviewed snapshot of
the completed run above, with the page's CC BY 3.0 credit and an excerpt of the captured text.
