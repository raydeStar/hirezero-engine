# Agent Rush submission draft · November 4, 2026

## Title

HireZero Engine — Research With Receipts

## Short description

A product marketer asks what a website promises. HireZero opens the approved site in an isolated browser and returns an answer, a screenshot and a receipt showing what it contacted and whether it shut down.

## Long description

Before drafting a campaign, a product marketer needs to know what the product page actually promises. Give HireZero one question and one public site. The model drafts the browser steps. The owner reviews the origins, limits and packet digest before Chromium starts. After the run, the marketer can read the answer against the captured page and inspect which requests the browser and host broker allowed or refused.

On November 4 a model-planned task read HireZero’s homepage in 14.429 seconds. The broker allowed ten requests, and both model calls used 2,575 tokens. In a separate web session, the owner selected a fixed packet for Choose a License. Chromium blocked Google Fonts, the broker separately refused a favicon, and the page still yielded the correct answer, MIT License. That browser took 10.899 seconds and the answer used 617 tokens. Both runs produced a screenshot and left zero labeled browser containers.

HireZero contributed the pinned workers, Docker adapters and public-web broker. During Agent Rush we added the agent workflow, one-use approval, request logging, token accounting, web desk and read-only judge gallery. The recordings use local gVisor and OpenRouter. Vultr Cloud Compute and Vultr Serverless Inference are the deployment targets. Hosted qualification and a public HTTPS URL are pending.

**Evidence for the listing.** The two run summaries cover the questions, captures, timing, usage, separate refusals and teardown. The code and tests show where the boundaries are enforced. [HireZero run][local-run] · [Web run][web-run] · [Agent][task] · [Scope tests][task-tests] · [Real-image qualification][real-test] · [Web desk][desk] · [Import][import]

## Vultr and deployment status

| Piece | What judges can inspect today | Hosted evidence |
| --- | --- | --- |
| Vultr VM backend | Scripts for provisioning a disposable Ubuntu VM, validating downloads and qualifying the browser on the VM's own engine. [Deployment runbook][deploy] · [Qualification script][qualify] | **Pending** — successful VM qualification and a hosted run. |
| Vultr Serverless Inference | The client defaults to Vultr. OpenRouter is a separately configured local stand-in. [Configuration tests][inference-tests] | **Pending** — receipt from a Vultr call. |
| Isolated sandbox | A fresh local qualification followed by two real public-site runs. The second shows browser-side and broker-side refusals. [HireZero run][local-run] · [Web run][web-run] · [Containment tests][containment] | **Pending** — same-host proof on Vultr. |
| Web app on Vultr | Implemented owner desk and separate judge gallery. The local HTTPS worker completed an approved browser run and the signed-out gallery displays both cases. [Web desk][desk] · [Design decision][web-decision] · [Web run][web-run] | **Pending** — deployed HTTPS web app and judge URL. |

## Listing fields

**Challenge** · Blast Radius Zero

**Participation mode** · [Owner to confirm online or on-site]

**Tags** · AI agents, browser automation, gVisor, sandboxing, Vultr, .NET

**GitHub** · [Event branch][branch]. Owner to publish the final revision and verify the links signed out.

**Demo application platform** · Vultr Cloud Compute, with Vultr Serverless Inference for model calls. Hosted deployment pending. [Deployment runbook][deploy] · [Inference configuration][inference-tests]

**Cover** · Use the prepared 16:9 image, `HireZero-Engine-Cover.png`.

**Presentation outline**

1. **The question** — What does HireZero help a user do? Give the agent one site to read. [Runner][runner]
2. **Scope before browsing** — The owner reviews origins, steps and limits before approving the digest. [Browser task][task] · [Approval tests][task-tests]
3. **Inspect the boundary** — In the Choose a License case, Chromium refused an outside font before the host broker. The broker separately refused the same-origin favicon. [Run evidence][web-run]
4. **Read the receipt** — Match the run's answer, capture, model usage and release status. Show the provider and host that actually ran it. [Browser task][task] · [Receipt writer][runner]
5. **From HireZero to Agent Rush** — The existing runtimes supplied the foundation. The event added the agent, model integration, accounting and VM tooling. Hosted proof is pending. [Import][import] · [Event changes][event] · [VM runbook][deploy]

**Demo link** · [Pending — owner-verified HTTPS web app on Vultr]

**Video** · `HireZero-Engine-Demo.mp4`, a narrated and captioned local cut, 4:50 and 17.72 MB. The live gallery walkthrough starts at 3:19 and the Choose a License containment chapter at 3:33. Final hosted footage and upload pending.

**Slides** · `HireZero-Engine.pdf` is the upload copy. `HireZero-Engine.pptx` is the editable source. Final upload pending.

See the [competition audit](history/2026-11-04-competition-audit.md) for the event requirements and public-link checks.

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
