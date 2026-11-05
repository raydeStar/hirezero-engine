# A real web run on Choose a License · November 4, 2026

An owner signed in to the local HTTPS web desk and approved a single question on Choose a License. Which license does the homepage describe as simple and permissive? The answer was **MIT License**. The capture shows the heading about permissive licensing and the corresponding MIT paragraph. The host was the qualified local WSL gVisor installation. OpenRouter supplied the answer. [Page](https://choosealicense.com/) · [Web worker](../../src/HireZero.Engine.Web/WebWorker.cs) · [Browser agent](../../src/HireZero.Agent/BrowserTask.cs)

| Stage | Observed |
| --- | --- |
| Prepare | The model first drafted a read of `main` on web revision `77de453`. The published page layout lacked that selector. The packet expired before execution. Drafting used 251 tokens (220 input, 31 output) in 2.773 s. |
| Review | Web revision `a11dabb` offered a fixed homepage packet. The owner reviewed navigation to the homepage, a read of `body`, a screenshot and one allowed origin. Limits were 90 seconds, 40 requests and 8 MiB. Preparing the replacement used zero model calls. |
| Execute | The owner approved digest prefix `45033dbf`. One execution captured the page in 10.899 s and produced a 55,496-byte JPEG. The worker observed and refused the off-site Google Fonts origin before dispatch to the host broker. |
| Inspect the network | The host broker allowed five same-origin GETs. It separately refused the same-origin favicon because that resource type was outside the page policy. The font and favicon were refused at different points. |
| Answer and release | The runner recorded release, then asked the model to answer from the page text. This took 2.633 s and used 617 tokens (489 input, 128 output). Counting the earlier unused draft brings the session total to 868. An independent Docker query returned zero browser containers with the installation label. |

The signed-out gallery serves two operator-selected summaries and their screenshots. The earlier [HireZero run](2026-11-04-local-browser-run.md) demonstrates a model-drafted packet and a product-research answer. The Choose a License run demonstrates a real third-party subresource refusal through the web UI using an owner-selected packet. The original run records, full event log, usage ledger, JPEGs and transcripts remain in private storage. The local recording identifies the host and provider. The public HTTPS service and Vultr inference receipt still require a run on the competition VM. [Gallery implementation](../../src/HireZero.Engine.Web/Gallery.cs) · [Deployment](../../deploy/vultr/web.md)

Page excerpts and the page pictured in the gallery are from *Choose a License*, curated by GitHub, Inc. and contributors. The gallery selects an excerpt and resizes the capture for display. [Source](https://choosealicense.com/) · [CC BY 3.0](https://creativecommons.org/licenses/by/3.0/)
