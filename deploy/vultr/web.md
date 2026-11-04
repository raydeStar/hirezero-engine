# Running the web desk

The same ASP.NET executable supports a read-only gallery, an offline rehearsal, and the qualified owner worker. Use local rehearsal to test the UI before provisioning. For a competition deployment, the VM must run the owner worker, inference orchestration and the qualified browser as well as serve the gallery.

## Local demo

From the repository root in PowerShell, choose a new private data directory outside the checkout. Enter a passphrase of at least 16 characters. The demo intentionally uses an offline model and browser. Run the following in one terminal and open the supplied loopback URL in a browser.

```powershell
$env:HIREZERO_WEB_PASSPHRASE = Read-Host -MaskInput 'Local rehearsal passphrase'
$webData = Join-Path $env:LOCALAPPDATA 'HireZero/DeskRehearsal'
$examples = (Resolve-Path 'deploy/demo-gallery').Path
dotnet run --project src/HireZero.Engine.Web -- --mode rehearsal --port 5077 --data $webData --gallery $examples
```

Open [the desk](http://127.0.0.1:5077), select **Owner desk**, sign in, draft a question, then inspect and approve the packet. The homepage template skips the model planning call. Read the resulting capture, network events and release record. Signing out invalidates the session. Refreshing a job reads its status. An interrupted job requires operator inspection before further paid work.

## Public gallery

Supply an absolute path to a separate directory that contains only reviewed snapshots. Each lowercase slug names a directory with `summary.json` and, optionally, a reviewed `capture.jpg` (up to 256 KiB). Copy *only* those two files after reviewing their content, including text visible in the image. The [fictional entry](../demo-gallery/fictional-paper/summary.json) shows the allowed fields. An operator can curate an excerpt, origins, timings, token totals and release status without exposing the raw receipt, job identifiers, queries or private engine binding. Replace a snapshot directory only after its files are ready.

A gallery launched alone can serve judges without any owner API, model key, passphrase or Docker access. Run it under a separate OS account with read access to the curated directory and application files only.

```sh
dotnet HireZero.Engine.Web.dll --mode gallery --port 5078 --gallery /srv/hirezero/gallery
```

## Hosted worker and TLS

1. Follow the [VM runbook](README.md) and qualify the browser on that VM. Capture the installation-bound qualification receipt in private storage. Confirm the Docker engine has no leftover browser jobs.
2. Publish from the checked-out entry revision with `dotnet restore src/HireZero.Engine.Web --locked-mode` and `dotnet publish src/HireZero.Engine.Web --no-restore -c Release -o /srv/hirezero/app`. Run both services with `/srv/hirezero/app` as their working directory so the static assets resolve.
3. Provision two service identities. Give only the private worker access to the local Docker endpoint, qualification receipt, an owner-only data directory (Unix mode 0700), and the model credential. Docker-socket access is host-privileged. Keep that worker account private. Pass the gallery only its curated directory and public origin. Use a single worker process and one private usage ledger across jobs. Use the same private owner account on restarts.
4. On both services, set `HIREZERO_WEB_ORIGIN` to the exact public HTTPS origin. Set `HIREZERO_WEB_PASSPHRASE`, `HIREZERO_BROWSER_RECEIPT`, `HIREZERO_INFERENCE_PROVIDER=vultr`, `VULTR_INFERENCE_API_KEY` and the [budget settings](../../.env.example) **only on the worker**. Supply secrets via protected service configuration outside the repo.
5. Start the gallery as above and the worker with the command below. Both listeners bind exclusively to loopback. Point DNS to the VM, provision a valid certificate (DNS validation works with 80 closed), and install the [example reverse-proxy config](engine-web.nginx.conf.example) after replacing the reserved hostname and certificate paths. Verify with `nginx -t` before reloading. Preserve the host and HTTPS scheme on both upstreams, and strip owner cookies and credentials on the gallery route.
6. With owner approval, allow inbound TCP 443 on the host firewall and the Vultr firewall protecting the VM. The bootstrap currently opens SSH only. Confirm port 443 from outside the VM, and confirm the worker and gallery ports are inaccessible externally. Keep the public demo available for the agreed judging window. The disposable-VM destroy command ends access to it.

```sh
cd /srv/hirezero/app
dotnet HireZero.Engine.Web.dll --mode worker --port 5077 --data /srv/hirezero/private/worker
```

**Local proxy check, November 4.** Nginx 1.30.6 loaded the example configuration in an isolated Linux container. A TLS request for each gallery route reached the gallery upstream with cookie, authorization and CSRF headers stripped. A gallery POST returned 403. Session reads and approval POSTs reached a separate owner test upstream with the expected headers and HTTPS scheme. The test used a temporary certificate and synthetic upstreams.

**Before submitting**, open a real, redacted gallery item in a signed-out browser on the public URL. Confirm private job routes reject the visitor. As owner, run one approved task on the qualified VM using Vultr inference. Verify the screenshot, answer, token usage and confirmed release against the VM evidence and inspect the engine for remaining browser resources. Update the README, submission text and video with those observations.

## When a request is interrupted

A pending packet is bound to one login and one worker instance. A replacement packet or restart expires the previous approval option. The job view can still show the available evidence. A persistent active-operation marker means execution, accounting or teardown may be unsettled. Preserve the private records and inspect the original engine, resource identity, usage and release first. Remove a stale marker only after operator reconciliation. Never reissue an uncertain request as a recovery action.
