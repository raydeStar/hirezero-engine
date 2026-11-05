# Agent Rush submission draft · November 5, 2026

## Title

HireZero Engine — Research With Receipts

## Short description

A product marketer asks what a website promises. HireZero opens the approved site in an isolated browser and returns an answer, a screenshot and a receipt showing what it contacted and whether it shut down.

## Long description

Before drafting a campaign, a product marketer needs to know what the product page actually promises. Give HireZero one question and one public site. The model drafts the browser steps. The owner reviews the origins, limits and packet digest before Chromium starts. After the run, the marketer can read the answer against the captured page and inspect which requests the browser and host broker allowed or refused.

On November 5 the whole stack ran on one Vultr Cloud Compute VM, with every model call on Vultr Serverless Inference (DeepSeek V4.1 Flash). The VM bootstrapped from pinned downloads and qualified its own gVisor browser image. Vultr's model drafted a three-step itinerary for Choose a License, and the owner approved it by digest. Chromium refused Google Fonts as off-site, the broker separately refused the favicon, and the page yielded the correct answer, MIT License. The browser took 11.4 seconds, both model calls used 1,080 tokens, teardown was confirmed and no container remained. The same flow then ran through the public HTTPS web desk.

HireZero contributed the pinned workers, Docker adapters and public-web broker. During Agent Rush we added the agent workflow, one-use approval, request logging, token accounting, web desk, read-only judge gallery and Vultr deployment. Earlier local runs on November 4 used gVisor under WSL with OpenRouter standing in for Vultr.

**Evidence for the listing.** The run summaries cover the questions, captures, timing, usage, separate refusals and teardown. The code and tests show where the boundaries are enforced. [Vultr runs][vultr-run] · [Local HireZero run][local-run] · [Local web run][web-run] · [Agent][task] · [Scope tests][task-tests] · [Real-image qualification][real-test] · [Web desk][desk] · [Import][import]

## Vultr and deployment status

| Piece | What judges can inspect | Hosted evidence, November 5 |
| --- | --- | --- |
| Vultr VM backend | Scripts for provisioning a disposable Ubuntu VM, validating downloads and qualifying the browser on the VM's own engine. [Deployment runbook][deploy] · [Qualification script][qualify] | One `vc2-2c-4gb` VM in Los Angeles: every pinned download matched, the browser image qualified on the VM's engine, and it serves the demo. [Vultr runs][vultr-run] |
| Vultr Serverless Inference | The client defaults to Vultr. OpenRouter is a separately configured local stand-in. [Configuration tests][inference-tests] | Smoke call and every agent call (drafts and answers) on `deepseek-v4.1-flash-1017`, with per-call token receipts. [Vultr runs][vultr-run] |
| Isolated sandbox | Containment tests plus real public-site runs with browser-side and broker-side refusals. [Containment tests][containment] · [Local web run][web-run] | Chromium under gVisor with no network refused Google Fonts; the broker refused the favicon; teardown confirmed with no container left. [Vultr runs][vultr-run] |
| Web app on Vultr | Owner desk and separate judge gallery. [Web desk][desk] · [Design decision][web-decision] | Live at [rush.hirezero.app][demo]: HTTPS, signed-out gallery with a reviewed snapshot of the Vultr run, passphrase-protected owner desk that completed an approved run. [Vultr runs][vultr-run] |

## Listing fields

**Challenge** · Blast Radius Zero

**Participation mode** · [Owner to confirm online or on-site]

**Tags** · AI agents, browser automation, gVisor, sandboxing, Vultr, .NET

**GitHub** · [Event branch][branch]. Owner to publish the final revision and verify the links signed out.

**Demo application platform** · Vultr Cloud Compute, with Vultr Serverless Inference for model calls. [Deployment runbook][deploy] · [Inference configuration][inference-tests]

**Cover** · Use the prepared 16:9 image, `HireZero-Engine-Cover.png`.

**Presentation outline**

1. **The question** — What does HireZero help a user do? Give the agent one site to read. [Runner][runner]
2. **Scope before browsing** — The owner reviews origins, steps and limits before approving the digest. [Browser task][task] · [Approval tests][task-tests]
3. **Inspect the boundary** — In the Choose a License case, Chromium refused an outside font before the host broker. The broker separately refused the same-origin favicon. [Run evidence][web-run]
4. **Read the receipt** — Match the run's answer, capture, model usage and release status. Show the provider and host that actually ran it. [Browser task][task] · [Receipt writer][runner]
5. **From HireZero to Agent Rush** — The existing runtimes supplied the foundation. The event added the agent, model integration, accounting, web desk and the Vultr deployment. [Import][import] · [Event changes][event] · [VM runbook][deploy] · [Vultr runs][vultr-run]

**Demo link** · [https://rush.hirezero.app][demo]

**Video** · `HireZero-Engine-Demo.mp4`, a narrated and captioned local cut, 4:50 and 17.72 MB. The live gallery walkthrough starts at 3:19 and the Choose a License containment chapter at 3:33. Re-record those chapters on the Vultr VM and the public desk before upload.

**Slides** · `HireZero-Engine.pdf` is the upload copy. `HireZero-Engine.pptx` is the editable source. Final upload pending.

See the [competition audit](history/2026-11-04-competition-audit.md) for the event requirements and public-link checks.

[vultr-run]: https://github.com/raydeStar/hirezero-engine/blob/vultr-agent-rush/docs/history/2026-11-05-first-vultr-runs.md
[demo]: https://rush.hirezero.app
[local-run]: https://github.com/raydeStar/hirezero-engine/blob/vultr-agent-rush/docs/history/2026-11-04-local-browser-run.md
[web-run]: https://github.com/raydeStar/hirezero-engine/blob/vultr-agent-rush/docs/history/2026-11-04-web-browser-run.md
[desk]: https://github.com/raydeStar/hirezero-engine/tree/vultr-agent-rush/src/HireZero.Engine.Web
[web-decision]: https://github.com/raydeStar/hirezero-engine/blob/vultr-agent-rush/docs/adr/0147-single-owner-demo-web-app.md
[task]: https://github.com/raydeStar/hirezero-engine/blob/vultr-agent-rush/src/HireZero.Agent/BrowserTask.cs
[runner]: https://github.com/raydeStar/hirezero-engine/blob/vultr-agent-rush/tools/HireZero.Engine.Run/Program.cs
[task-tests]: https://github.com/raydeStar/hirezero-engine/blob/vultr-agent-rush/tests/HireZero.Engine.Tests/BrowserTaskTests.cs
[containment]: https://github.com/raydeStar/hirezero-engine/blob/vultr-agent-rush/tests/HireZero.Engine.Tests/BrowserContainmentTests.cs
[broker-tests]: https://github.com/raydeStar/hirezero-engine/blob/vultr-agent-rush/tests/HireZero.Engine.Tests/BrowserNetworkLogTests.cs
[real-test]: https://github.com/raydeStar/hirezero-engine/blob/vultr-agent-rush/tests/HireZero.Engine.Tests/BrowserRuntimeTests.cs
[history]: https://github.com/raydeStar/hirezero-engine/blob/vultr-agent-rush/docs/history/2026-10-26-local-browser-qualification.md
[import]: https://github.com/raydeStar/hirezero-engine/commit/0b9c5532bd23352d1e4e00482a74143f6ac97021
[event]: https://github.com/raydeStar/hirezero-engine/compare/0b9c553...180aa21
[inference-tests]: https://github.com/raydeStar/hirezero-engine/blob/vultr-agent-rush/tests/HireZero.Engine.Tests/InferenceTests.cs
[deploy]: https://github.com/raydeStar/hirezero-engine/blob/vultr-agent-rush/deploy/vultr/README.md
[qualify]: https://github.com/raydeStar/hirezero-engine/blob/vultr-agent-rush/deploy/vultr/qualify.sh
[broker]: https://github.com/raydeStar/hirezero-engine/blob/vultr-agent-rush/src/HireZero.Integrations.Web/PublicBrowserNetwork.cs

[branch]: https://github.com/raydeStar/hirezero-engine/tree/vultr-agent-rush
