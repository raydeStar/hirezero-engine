# Demo script · 4 minutes 30 seconds

Use one approved run for the capture, network decisions, teardown and token totals. Keep the same job ID and digest in frame across cuts. Set up the qualified host, the SDK path, the private runs directory (`DEMO_RUNS`) and the provider credentials off camera. Crop private paths, keys and unrelated records.

The [November 4 local evidence](history/2026-11-04-local-browser-run.md) includes one screenshot and a transcript that can be shown together. Shots 1, 2 and 8 can be recorded now. Shots 3–7 can use the owner's local qualification host with the caption **Local WSL · OpenRouter stand-in** throughout. To label those shots **Vultr VM · Vultr Serverless Inference**, capture a separately approved run on the qualified VM. A hosted web app needs its own live capture before it appears in the film. [Runner](../tools/HireZero.Engine.Run/Program.cs) · [VM qualification and inference setup](../deploy/vultr/README.md)

### 0:00–0:15 · The question (15 s, any host)

**Picture.** Start on the owner's question, “What does HireZero help a user do?” Reveal the single permitted site, `https://hirezero.app`.

**Narration.** “When I ask a browser agent to read one page, how do I know what else it contacted and whether its browser is still running?”

### 0:15–0:45 · Who can reach the internet? (30 s, any host)

**Picture.** Use the [README architecture diagram](../README.md). Trace the model and agent, then the isolated browser's request to the host broker. End at the receipts. For local footage, label the model in the diagram as the Vultr deployment target.

**Narration.** “The model drafts a list of steps. I approve the sites and limits on the host. Chromium runs under gVisor with networking disabled. To load a resource it must ask the host broker, which checks the approved origins and the site's crawl policy. We can inspect what the browser observed and what the broker actually handled.” [Broker](../src/HireZero.Integrations.Web/PublicBrowserNetwork.cs) · [Browser driver](../runtime/browser/driver.mjs)

### 0:45–1:35 · Approve this packet (50 s, qualified local host or Vultr VM)

**Picture.** Show the actual model/provider line and the runner's itinerary source. Hold on steps, approved origins and resource limits. Type the first eight characters of the displayed packet digest only after owner review. Record the original session or play its recording; each fresh invocation can call the model. [Runner arguments and approval prompt](../tools/HireZero.Engine.Run/Program.cs)

```sh
dotnet run --project tools/HireZero.Engine.Run -- --goal "What does HireZero help a user do?" --site https://hirezero.app --runs "$DEMO_RUNS"
```

**Narration.** “Here is the goal and the only origin I am allowing. The screen tells me whether the model produced the plan or the fixed template took over. I can inspect the navigation, the read selector and the time allowance. Typing this digest prefix authorizes the packet on screen.”

### 1:35–2:05 · Read the result (30 s, same run)

**Picture.** Match the printed read step and URL to its capture in `execution.json`. Show the corresponding `capture-…jpg` and the relevant sentence in `answer.md` if present. Use the recorded outcome if a read or answer failed. [Capture and answer writer](../src/HireZero.Agent/BrowserTask.cs)

**Narration.** “Here is what the approved read captured. Compare the visible page text with the answer to our opening question. The model received the capture as text. It had no browser tool for following another instruction on the page.”

### 2:05–2:40 · Inspect the refusal (35 s, same run)

**Picture.** In the same job's `receipt.json`, open `browser.blockedOffSiteOrigins` and `network.blocked`. The November 4 run recorded ten allowed broker requests and zero off-site refusals. For this footage, show the actual counts and the separately labeled test below. If a later approved run records a refusal, show the matching `BLOCKED off-site` console line and point to the origin. A browser observation can precede any broker call. If neither recorded an off-site refusal, show the actual counts and mark the live refusal shot pending. A separate screen of the broker's off-site unit test must be captioned **offline test**. [Runner receipt](../tools/HireZero.Engine.Run/Program.cs) · [Broker refusal test](../tests/HireZero.Engine.Tests/BrowserNetworkLogTests.cs)

**Narration.** If observed: “This origin was outside my approved site. The receipt identifies where the refusal happened. The page capture lets us see how much of the answer survived.” Otherwise: “This run recorded no off-site refusal. Here is the separate offline test that sends an off-site request to the broker and checks it never reaches the transport.”

### 2:40–3:15 · Check the empty host (35 s, same Docker engine)

**Picture.** Hold on the actual `Teardown:` line, then on `teardown.confirmed` and the associated resource ID in the receipt. From the same engine run the following command. Leave the returned prompt visible so an empty result is legible. [Qualification script's leftover check](../deploy/vultr/qualify.sh)

```sh
docker ps -aq --filter label=io.hirezero.browser.installation
```

**Narration.** On confirmed release and empty output: “The engine recorded release of this browser. This second check lists running and stopped containers carrying its browser-installation label. The prompt came back with no IDs.” If release failed or any ID appears, report that outcome on screen.

### 3:15–3:45 · Account for the model calls (30 s, same run)

**Picture.** Show `provider`, `model`, `modelCalls` and `tokensThisRun` in the same receipt. Highlight the purpose and outcome of each call, then its actual token fields. For the November 4 run, show 353 draft tokens and 2,222 answer tokens, 2,575 total. [Usage accounting](../src/HireZero.Integrations.Inference/InferenceBudget.cs) · [Receipt fields](../tools/HireZero.Engine.Run/Program.cs)

**Narration.** “The receipt tells us who served the request and what each call was for. I can read the reported input and output tokens alongside the run's charged total. If a provider's outcome is unknown, the budget keeps the reservation and the client does not automatically retry.”

### 3:45–4:30 · What the team built (45 s, any host)

**Picture.** Show the [original HireZero engine import](https://github.com/raydeStar/hirezero-engine/commit/0b9c5532bd23352d1e4e00482a74143f6ac97021), then the [Agent Rush changes through Nov 3](https://github.com/raydeStar/hirezero-engine/compare/0b9c553...180aa21). Finish on the recording's actual environment and the status of the hosted demo. Show a deployed HTTPS page only when the web app is running on Vultr.

**Narration.** “HireZero supplied the pinned browser and script workers, the Docker adapters and the public-web broker. For the November 3–8 Agent Rush we connected the browser to a model, added owner approval and token accounting, recorded network decisions, and scripted deployment and qualification on a disposable Vultr VM. The receipt and the final host check tell you exactly which environment this demonstration exercised.”
