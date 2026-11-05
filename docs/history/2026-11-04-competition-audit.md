# Agent Rush competition audit · November 4, 2026

**Track recommendation.** Enter Challenge 1, *Blast Radius Zero*. Two qualified local browser runs, a working owner desk, a signed-out evidence gallery and a narrated demo are ready. The hosted stack and public entry still need proof. [HireZero](2026-11-04-local-browser-run.md) · [Choose a License](2026-11-04-web-browser-run.md) · [Event playbook][event] · [Sponsor requirements][sponsor]

## Entry and timing

- **Target Saturday, November 7.** Keep the owner's Sunday, November 8 cutoff at **3:00 PM MST (22:00 UTC)**. The public LabLab event data lists an end of **23:00 UTC (4:00 PM MST)**, and Vultr's Sunday agenda places the end of submissions at that hour. Recheck the enrolled team's countdown before upload. The rule book describes a short grace period and directs teams with technical problems to reach organizers before close. [Event][event] · [Sponsor agenda][sponsor] · [Rule book][rules]
- **Submit through the enrolled LabLab team.** The event allows teams of 1–6. Check connected Discord membership, enrollment, participation mode and submitter permissions. The team admin can designate up to three other submitters. Posting on Vultr Community is optional. [Event][event] · [Getting started][start]
- **Show the lineage.** The event calls for original, MIT-compliant work. The terms require open source and the rights to use submitted material. The checked event, rule book and terms specify neither an all-new-code rule nor blanket permission for prebuilt entries. Identify the inherited workers, adapters and broker, preserve their notices and show the dated diff for the agent work. The owner should verify rights to the source, images and recorded content before submitting. Entrants retain ownership while granting LabLab broad rights to use submitted content. [Terms §§4, 16][terms] · [Rule book][rules] · [Import][import] · [License](../../LICENSE) · [Dependency notices](../../THIRD_PARTY_NOTICES.md)

## What a judge can check

| Priority | Evidence on November 4 | What closes the gap |
| --- | --- | --- |
| **Required · VM, web, inference and sandbox** | The [web app](../../src/HireZero.Engine.Web/) ran the owner-approved [Choose a License packet](2026-11-04-web-browser-run.md) under local gVisor with OpenRouter. | Qualify the Vultr VM itself, serve the HTTPS app there, route all agent calls through Vultr Serverless Inference and verify the approved job, usage and cleanup. [Web deployment](../../deploy/vultr/web.md) · [Sponsor][sponsor] |
| **Required · public demo and recorded containment** | The signed-out local gallery has both real cases. The narrated recording shows Chromium refusing a Google Fonts request on Choose a License and the broker separately refusing a same-origin favicon. | Open the eventual public URL signed out. Capture the hosted task and its provider and swap that portion into the submission version of the recording. [Web evidence](2026-11-04-web-browser-run.md) · [Event][event] |
| **Required · public repository and README** | Anonymous GitHub metadata showed a public repo with default branch `main` and `vultr-agent-rush` at `180aa21`. The local November 4 web changes have yet to be published. | Publish the entry revision and open it signed out. Check setup, the Vultr services actually used, deployment diagram, use case and evidence links. [Public metadata][repo-api] · [Branch metadata][branch-api] · [README](../../README.md) · [Event][event] |
| **Required · assets and form** | The [listing draft](../submission.md), 16:9 cover, ten-slide editable deck and PDF, and captioned MP4 are ready for local review. | Upload the final recording, slides and cover, enter the platform and HTTPS URL, and verify the public links. The event form takes precedence over default guidance. [Submission guide][guide] · [Participation walkthrough][guidelines] |

**Execution patterns.** One or both paths may be used. The browser pattern depicts a model plan, Playwright, screenshots, vision verification and human approval. This implementation captures screenshots for human review and answers from captured text. It has no demonstrated vision-model check. The required stack lists isolation and Vultr inference, while the pattern supplies details a browser judge may ask about. Similarly, a script retry loop and an instance per job are examples. Describe the actual behavior, including one-use approval and how an uncertain outcome is handled. [Challenge playbook][event] · [Browser agent](../../src/HireZero.Agent/BrowserTask.cs) · [Script command](../../tools/HireZero.Engine.Run/ScriptCommand.cs)

## Review rehearsal

The event names four judging criteria without publishing numerical weights. The rule book illustrates 1–5 scoring. Use these questions for a rehearsal and keep the required deployment proof separate from any qualitative rating. [Event criteria][event] · [Rule book][rules]

| Criterion | Question for the reviewer | Current evidence and what to show |
| --- | --- | --- |
| Application of technology | Can I trace model planning to a real, bounded action and its output? | Match the approved digest, captured text and screenshot, model identity, token count and release on one run. The local run establishes that flow. Hosted integration remains the decisive gap. [Run summary](2026-11-04-local-browser-run.md) |
| Presentation | Can I follow the question, approve the plan and interpret the refusal? | Show the HireZero model plan and then identify the Choose a License web run when switching. Its Network tab and capture explain the boundary without a terminal. [Run evidence](2026-11-04-web-browser-run.md) |
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
