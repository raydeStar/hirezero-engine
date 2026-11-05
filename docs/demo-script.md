# Demo script · 4 minutes 30 seconds

**Recording update, November 5.** Record on the Vultr VM and the public desk at [rush.hirezero.app](https://rush.hirezero.app). Every model call there goes to Vultr Serverless Inference, so caption hosted shots **Vultr VM · Vultr Serverless Inference**. The November 4 local cut (WSL, OpenRouter stand-in) can still supply the "what we built" chapter. [Vultr runs](history/2026-11-05-first-vultr-runs.md) · [Local web run](history/2026-11-04-web-browser-run.md)

Use one approved run for the capture, network decisions, teardown and token totals, and keep its job ID and digest in frame across cuts. Each fresh draft calls the model and needs a new approval, so record a run from start to finish rather than replaying an approval. Crop the SSH key path, the passphrase field and any unrelated records.

Choose a License is the demo site: its homepage loads a Google Fonts stylesheet, which the browser refuses as off-site, and the broker refuses its favicon. Both refusals appear in every run so far.

### 0:00–0:15 · The question (15 s)

**Picture.** The signed-out gallery at `https://rush.hirezero.app`, then the owner's question, "Which license does the page suggest for someone who wants it simple and permissive?", and the single permitted site, `https://choosealicense.com`.

**Narration.** "When I ask a browser agent to read one page, how do I know what else it contacted and whether its browser is still running?"

### 0:15–0:45 · Who can reach the internet? (30 s)

**Picture.** The [README architecture diagram](../README.md). Trace the model on Vultr and the agent, then the isolated browser's request to the host broker. End at the receipts.

**Narration.** "Vultr's model drafts a list of steps. I approve the sites and limits on the host. Chromium runs under gVisor with networking disabled. To load a resource it must ask the host broker, which checks the approved origins and the site's crawl policy. We can inspect what the browser observed and what the broker actually handled." [Broker](../src/HireZero.Integrations.Web/PublicBrowserNetwork.cs) · [Browser driver](../runtime/browser/driver.mjs)

### 0:45–1:35 · Approve this packet (50 s)

**Picture.** Either the owner desk (sign in, enter the goal and site, review, approve) or the terminal on the VM. Hold on the provider line, the itinerary's source, steps, approved origin and limits. Approve only after reading them.

```sh
ssh -t -i ~/.ssh/hirezero_vultr root@<vm-ip> hz-demo
```

`hz-demo` is a VM-local wrapper for `dotnet run --project tools/HireZero.Engine.Run -- --goal "…" --site https://choosealicense.com` with the Vultr key loaded from a root-only file. The prompt names the eight characters to type. [Runner and approval prompt](../tools/HireZero.Engine.Run/Program.cs)

**Narration.** "Here is the goal and the only origin I am allowing. The screen says the model produced this plan. I can inspect the navigation, the read selector and the time allowance. Typing this code authorizes exactly this packet, once."

### 1:35–2:05 · Read the result (30 s, same run)

**Picture.** The answer beside the screenshot: the "I want it simple and permissive" column names the MIT License. [Capture and answer writer](../src/HireZero.Agent/BrowserTask.cs)

**Narration.** "Here is what the approved read captured, and the answer drawn from it. The model received the page as text. It had no browser tool for following any instruction on the page."

### 2:05–2:40 · Inspect the refusals (35 s, same run)

**Picture.** The `BLOCKED off-site: https://fonts.googleapis.com` and `BLOCKED refused: GET …/favicon.ico` lines, then the matching `browser.blockedOffSiteOrigins` and `network.blocked` entries in `receipt.json`. [Receipt writer](../tools/HireZero.Engine.Run/Program.cs) · [Refusal tests](../tests/HireZero.Engine.Tests/BrowserNetworkLogTests.cs)

**Narration.** "The page asked for a font from Google. The browser refused it before anything left the sandbox, because Google isn't an approved site. The broker separately refused the favicon as outside the page's scope. The page still answered the question."

### 2:40–3:15 · Check the empty host (35 s, same VM)

**Picture.** Hold on the `Teardown: confirmed` line and the resource ID in the receipt, then run this on the VM and leave the empty result visible. [Qualification script's leftover check](../deploy/vultr/qualify.sh)

```sh
docker ps -aq --filter label=io.hirezero.browser.installation
```

**Narration.** "The engine recorded release of this browser. This second check lists running and stopped containers carrying its label. The prompt came back with no IDs." If release failed or any ID appears, report that outcome on screen.

### 3:15–3:45 · Account for the model calls (30 s, same run)

**Picture.** `provider`, `model`, `modelCalls` and `tokensThisRun` in the same receipt. The November 5 run used 468 draft tokens and 612 answer tokens, 1,080 in total, about a tenth of a cent on Vultr. [Usage accounting](../src/HireZero.Integrations.Inference/InferenceBudget.cs)

**Narration.** "The receipt names who served each call, what it was for and what it cost in tokens. If a provider's outcome is unknown, the budget keeps the reservation and nothing is retried automatically."

### 3:45–4:30 · What the team built (45 s)

**Picture.** The [original HireZero engine import](https://github.com/raydeStar/hirezero-engine/commit/0b9c5532bd23352d1e4e00482a74143f6ac97021), then the [Agent Rush changes](https://github.com/raydeStar/hirezero-engine/compare/0b9c553...vultr-agent-rush). Finish on the public desk at `https://rush.hirezero.app`.

**Narration.** "HireZero supplied the pinned browser and script workers, the Docker adapters and the public-web broker. For the November 3–8 Agent Rush we connected the browser to Vultr's model, added owner approval and token accounting, recorded network decisions, and deployed it on a Vultr VM with a public web desk. The receipt and the final host check show exactly what this run touched."
