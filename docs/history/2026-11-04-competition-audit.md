# Agent Rush competition audit · November 4, 2026

**Decision for the owner.** Enter Challenge 1, *Blast Radius Zero*, with the browser as the main demonstration. The audited revision `447ee7c` includes a summary of a real local browser completing a public-site task and releasing its container. The four Vultr-related requirements still need evidence from the same hosted system. A working local web app, a passing unit test or a local OpenRouter receipt can substantiate the engineering while credits are pending. [Local run](2026-11-04-local-browser-run.md) · [Event playbook][event] · [Sponsor's required stack][sponsor]

## Entry and timing

- **Target Saturday, November 7.** Keep the owner's Sunday, November 8 cutoff at **3:00 PM MST (22:00 UTC)**. The public LabLab event data lists an end of **23:00 UTC (4:00 PM MST)**, and Vultr's Sunday agenda places the end of submissions at that hour. Recheck the enrolled team's countdown before upload. The rule book describes a short grace period and directs teams with technical problems to reach organizers before close. [Event][event] · [Sponsor agenda][sponsor] · [Rule book][rules]
- **Submit through the enrolled LabLab team.** The event allows teams of 1–6. Check connected Discord membership, enrollment, participation mode and submitter permissions. The team admin can designate up to three other submitters. Posting on Vultr Community is optional. [Event][event] · [Getting started][start]
- **Show the lineage.** The event calls for original, MIT-compliant work. The terms require open source and the rights to use submitted material. The checked event, rule book and terms specify neither an all-new-code rule nor blanket permission for prebuilt entries. Identify the inherited workers, adapters and broker, preserve their notices and show the dated diff for the agent work. The owner should verify rights to the source, images and recorded content before submitting. Entrants retain ownership while granting LabLab broad rights to use submitted content. [Terms §§4, 16][terms] · [Rule book][rules] · [Import][import] · [License](../../LICENSE) · [Dependency notices](../../THIRD_PARTY_NOTICES.md)

## What a judge can check

| Priority | Evidence on November 4 | What closes the gap |
| --- | --- | --- |
| **Required · VM, web, inference and sandbox** | Deployment scripts, [Ubuntu verification](2026-11-04-bootstrap-dry-run.md), a [local qualified browser run](2026-11-04-local-browser-run.md) and an [accepted web design](../adr/0147-single-owner-demo-web-app.md). | Qualify the browser on the Vultr VM that orchestrates the task. Serve the working web app there over HTTPS. Capture one owner-approved workflow with all model calls routed through Vultr Serverless Inference. Verify the result, usage and cleanup on that VM. [Deployment runbook](../../deploy/vultr/README.md) · [Sponsor][sponsor] |
| **Required · public demo and recorded containment** | The local live task recorded ten allowed requests and zero refusals. A separate real Chromium qualification used a fictional page that attempted an outside-origin fetch. A timed [shot list](../demo-script.md) distinguishes the two. | Show judges how to explore one read-only run from goal to result without a login. Film an observed refusal, deadline enforcement or teardown. Label the host, provider and source of each cut. A staged qualification can demonstrate containment if it is identified as such. [Real-image check](../../tests/HireZero.Engine.Tests/BrowserRuntimeTests.cs) · [Event][event] |
| **Required · public repository and README** | Anonymous GitHub REST returned a public repo with default branch `main`. At audit time the public `vultr-agent-rush` head was `180aa21` and the local audited head was `447ee7c`. The public branch preceded the November 4 local changes. | After owner publication, open the exact entry revision signed out. Check setup, Vultr services actually used, deployment diagram, use case and evidence links. The event permits the README to carry the architecture explanation. [Public metadata][repo-api] · [Branch metadata][branch-api] · [README](../../README.md) · [Event][event] |
| **Required · assets and form** | The [listing draft](../submission.md) and [timed script](../demo-script.md) supply the story. | Upload the completed recording and slide PDF, provide the cover and platform field, and verify the submitted links. The event form takes precedence over default guidance. [Submission guide][guide] · [Participation walkthrough][guidelines] |

**Execution patterns.** One or both paths may be used. The browser pattern depicts a model plan, Playwright, screenshots, vision verification and human approval. This implementation captures screenshots for human review and answers from captured text. It has no demonstrated vision-model check. The required stack lists isolation and Vultr inference, while the pattern supplies details a browser judge may ask about. Similarly, a script retry loop and an instance per job are examples. Describe the actual behavior, including one-use approval and how an uncertain outcome is handled. [Challenge playbook][event] · [Browser agent](../../src/HireZero.Agent/BrowserTask.cs) · [Script command](../../tools/HireZero.Engine.Run/ScriptCommand.cs)

## Review rehearsal

The event names four judging criteria without publishing numerical weights. The rule book illustrates 1–5 scoring. Use these questions for a rehearsal and keep the required deployment proof separate from any qualitative rating. [Event criteria][event] · [Rule book][rules]

| Criterion | Question for the reviewer | Current evidence and what to show |
| --- | --- | --- |
| Application of technology | Can I trace model planning to a real, bounded action and its output? | Match the approved digest, captured text and screenshot, model identity, token count and release on one run. The local run establishes that flow. Hosted integration remains the decisive gap. [Run summary](2026-11-04-local-browser-run.md) |
| Presentation | Can I follow the question, approve the plan and interpret the refusal without the presenter translating terminal logs? | Use the same job across the story and label any qualification cut. Show the public URL in a signed-out browser. [Shot list](../demo-script.md) |
| Business value | Who would use the result, and what could they do next? | A product marketer checks what a public page promises before drafting copy. They can compare the answer with the captured sentence before using it. Time saved, demand, willingness to pay and customer adoption are unmeasured. [Observed task](2026-11-04-local-browser-run.md) |
| Originality | Which new behavior did the team demonstrate during this event? | Show how the new draft, digest approval, answer and receipt flow uses the old worker and broker. A reviewer can change a displayed digest and see approval fail in the test. [Original import][import] · [Scope tests](../../tests/HireZero.Engine.Tests/BrowserTaskTests.cs) |

## Owner submission checklist

- [ ] Verify the team, named submitter, participation mode, track and live form limits. Standard lengths are 5–50 characters for the title, 50–255 for the summary and 600–2,000 for the description. [Submission guide][guide] · [Getting started][start]
- [ ] Confirm rights and attribution. Compare the original import to the submitted event revision. Verify every cited file and the README from a signed-out session. [Terms][terms] · [Public branch][branch]
- [ ] Once authorized to deploy, qualify the actual VM. Record its web flow, real provider, permitted origin, one containment event, result and teardown. Confirm the public judge route works without granting visitors spending authority. Agree how long the URL will be kept available for evaluation before destroying the VM. [Web decision](../adr/0147-single-owner-demo-web-app.md) · [Sponsor requirements][sponsor]
- [ ] Use a 16:9 cover. Supply slides as PDF and the recording as MP4 or MOV under 300 MB. Keep it under five minutes. The event lists the cover despite the generic guide making it optional. Use the upload or URL control on the actual form and test any linked file without owner access. [Event][event] · [Submission guide][guide]
- [ ] Paste the reviewed listing, the actual hosting platform and the working HTTPS application URL. Confirm the media plays, submit on LabLab and save the confirmation privately. [Listing draft](../submission.md) · [Submission guide][guide]

[event]: https://lablab.build/ai-hackathons/vultr-hackathon
[sponsor]: https://community.vultr.com/e/vultr-agent-rush-hackathon
[guide]: https://lablab.build/delivering-your-hackathon-solution
[rules]: https://lablab.build/hackathon-rules
[guidelines]: https://lablab.build/ai-articles/hackathon-guidelines
[start]: https://lablab.build/getting-started-guide
[terms]: https://lablab.build/terms-of-use
[repo-api]: https://api.github.com/repos/raydeStar/hirezero-engine
[branch-api]: https://api.github.com/repos/raydeStar/hirezero-engine/branches/vultr-agent-rush
[branch]: https://github.com/raydeStar/hirezero-engine/tree/vultr-agent-rush
[import]: https://github.com/raydeStar/hirezero-engine/commit/0b9c5532bd23352d1e4e00482a74143f6ac97021
