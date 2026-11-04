# Agent Rush submission draft · November 4, 2026

## Title

HireZero Engine — Research With Receipts

## Short description

A product marketer asks what a website promises. HireZero opens the approved site in an isolated browser and returns an answer, a screenshot and a receipt showing what it contacted and whether it shut down.

## Long description

Before drafting a campaign, a product marketer needs to know what the product page actually promises. Give HireZero one question and one public site. The model drafts the browser steps. The owner reviews the origins, limits and packet digest before Chromium starts. After the run, the marketer can read the answer against the captured page and inspect which requests the browser and host broker allowed or refused.

On November 4 the agent read the HireZero homepage to answer what the product helps a user do. The local browser captured the page in 14.429 seconds. Its broker allowed ten requests, the run reported no refusals, and a separate host check found zero containers bearing the engine's browser label. The two OpenRouter calls used 2,575 tokens. A separate qualification on the same engine exercised a fictional page that attempted an off-site fetch.

HireZero contributed the pinned workers, Docker adapters and public-web broker. During Agent Rush we added the model-driven workflow, one-use packet approval, request logging, token accounting and VM setup. Vultr Cloud Compute is the deployment target and Vultr Serverless Inference is the configured provider. Hosted qualification, a public HTTPS demo and a Vultr inference receipt are pending.

**Evidence for the listing.** The run summary records the task, capture, timings, tokens and teardown. The qualification test checks the off-site refusal, the scope tests exercise the approval boundary, and the import and event changes establish lineage. [Local run][local-run] · [Real-image qualification][real-test] · [Scope tests][task-tests] · [Agent][task] · [Runner][runner] · [Import][import] · [Event changes through November 3][event]

## Vultr and deployment status

| Piece | What judges can inspect today | Hosted evidence |
| --- | --- | --- |
| Vultr VM backend | Scripts for provisioning a disposable Ubuntu VM, validating downloads and qualifying the browser on the VM's own engine. [Deployment runbook][deploy] · [Qualification script][qualify] | **Pending** — successful VM qualification and a hosted run. |
| Vultr Serverless Inference | The client defaults to Vultr. OpenRouter is a separately configured local stand-in. [Configuration tests][inference-tests] | **Pending** — receipt from a Vultr call. |
| Isolated sandbox | Pinned worker, enforced container profile, offline broker/containment tests and a fresh local real-image qualification followed by one public-site run. [Local run][local-run] · [Containment tests][containment] · [Broker tests][broker-tests] · [Local history][history] | **Pending** — same-host proof on Vultr. |
| Web app on Vultr | The current interface is the terminal runner. The owner selected a single-owner HTTPS app with a read-only judge gallery. [Runner][runner] · [Design decision][web-decision] | **Pending** — deployed HTTPS web app and judge URL. |

## Listing fields

**Challenge** · Blast Radius Zero

**Participation mode** · [Owner to confirm online or on-site]

**Tags** · AI agents, browser automation, gVisor, sandboxing, Vultr, .NET

**GitHub** · [Event branch][branch]. Owner to publish the final revision and verify the links signed out.

**Demo application platform** · Vultr Cloud Compute, with Vultr Serverless Inference for model calls. Hosted deployment pending. [Deployment runbook][deploy] · [Inference configuration][inference-tests]

**Cover caption** · One approved website. What did the browser actually read? Use a redacted capture beside its itinerary digest and release status when available. [Capture writer][task] · [Receipt fields][runner]

**Slide copy**

1. **The question** — What does HireZero help a user do? Give the agent one site to read. [Runner][runner]
2. **Scope before browsing** — The owner reviews origins, steps and limits before approving the digest. [Browser task][task] · [Approval tests][task-tests]
3. **Inspect the boundary** — An isolated browser asks the host broker for public resources. An off-site request gets a traceable refusal in the broker test. [Broker][broker] · [Refusal test][broker-tests]
4. **Read the receipt** — Match the run's answer, capture, model usage and release status. Show the provider and host that actually ran it. [Browser task][task] · [Receipt writer][runner]
5. **From HireZero to Agent Rush** — The existing runtimes supplied the foundation. The event added the agent, model integration, accounting and VM tooling. Hosted proof is pending. [Import][import] · [Event changes][event] · [VM runbook][deploy]

**Demo link** · [Pending — owner-verified HTTPS web app on Vultr]

**Video** · [Pending — uploaded recording with provider and host identified]

**Slide PDF and 16:9 cover** · [Pending — final upload]

See the [competition audit](history/2026-11-04-competition-audit.md) for the event requirements and public-link checks.

[local-run]: https://github.com/raydeStar/hirezero-engine/blob/vultr-agent-rush/docs/history/2026-11-04-local-browser-run.md
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
