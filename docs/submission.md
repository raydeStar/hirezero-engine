# Agent Rush submission draft · November 4, 2026

## Title

HireZero Engine — A Browser Agent With Receipts

## Short description

Give the agent a question and a public site. Review its itinerary, approve the exact packet, then inspect the captured page, the answer, the network decisions, the token usage and whether the browser was removed. [Browser task][task] · [Receipt writer][runner]

## Long description

Suppose you ask, “What does HireZero help a user do?” That question authorizes reading a site. It says nothing about loading a third-party script or opening another site because the page asks. HireZero Engine makes the scope visible. The host shows the steps, permitted origins, limits and packet digest before the browser starts. If the digest differs, execution stops. After the read, the agent saves the capture and asks the model to answer from that text. [Browser task][task] · [Approval and execution tests][task-tests]

A judge can inspect what each layer checked. One test plants a fictional provider key on the host and examines the container arguments and worker input for it. Another checks the gVisor runtime, disabled networking, 512 MiB memory limit, one CPU and removal on release. A broker test asks for an off-site URL and asserts that the transport never receives it. The runner records the qualified image, the request decisions seen by the broker and the release result. A separate real-image suite exercises Chromium under gVisor. The earlier HireZero local qualification used fictional pages. [Containment tests][containment] · [Broker tests][broker-tests] · [Receipt writer][runner] · [Real-image test][real-test] · [Local qualification history][history]

During the November 3–8 build window we took HireZero's existing sandbox workers, Docker adapters and public-web broker and built a browser agent around them. Event work added the inference client, per-run and monthly token limits, request logging, digest approval, a text-answer step and VM setup scripts. The cost record charges unknown outcomes conservatively, and the client does not replay an uncertain call. The present judge entry point is the terminal runner. [Original import][import] · [Event changes through Nov 3][event] · [Budget and retry tests][inference-tests] · [Runner][runner]

## Vultr and deployment status

| Piece | What judges can inspect today | Hosted evidence |
| --- | --- | --- |
| Vultr VM backend | Scripts for provisioning a disposable Ubuntu VM, validating downloads and qualifying the browser on the VM's own engine. [Deployment runbook][deploy] · [Qualification script][qualify] | **Pending** — successful VM qualification and a hosted run. |
| Vultr Serverless Inference | The client defaults to Vultr. OpenRouter is a separately configured local stand-in. [Configuration tests][inference-tests] | **Pending** — receipt from a Vultr call. |
| Isolated sandbox | Pinned worker, enforced container profile, offline broker/containment tests and a historical local real-image qualification. [Containment tests][containment] · [Broker tests][broker-tests] · [Local history][history] | **Pending** — same-host proof on Vultr. |
| Web app on Vultr | The current interface is the terminal runner. [Runner][runner] | **Pending** — deployed HTTPS web app and judge URL. |

## Listing fields

**Tags** · AI agents, browser automation, gVisor, sandboxing, Vultr, .NET

**Cover caption** · One approved website. What did the browser actually read? Use a redacted capture beside its itinerary digest and release status when available. [Capture writer][task] · [Receipt fields][runner]

**Slide copy**

1. **The question** — What does HireZero help a user do? Give the agent one site to read. [Runner][runner]
2. **Scope before browsing** — The owner reviews origins, steps and limits before approving the digest. [Browser task][task] · [Approval tests][task-tests]
3. **Inspect the boundary** — An isolated browser asks the host broker for public resources. An off-site request gets a traceable refusal in the broker test. [Broker][broker] · [Refusal test][broker-tests]
4. **Read the receipt** — Match the run's answer, capture, model usage and release status. Show the provider and host that actually ran it. [Browser task][task] · [Receipt writer][runner]
5. **From HireZero to Agent Rush** — The existing runtimes supplied the foundation. The event added the agent, model integration, accounting and VM tooling. Hosted proof is pending. [Import][import] · [Event changes][event] · [VM runbook][deploy]

**Demo link** · [Pending — owner-verified HTTPS web app on Vultr]

**Video** · [Pending — published recording with provider and host identified]

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
