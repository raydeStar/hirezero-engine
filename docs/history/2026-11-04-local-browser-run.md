# Local qualification and one public-site run · November 4, 2026

The owner selected `https://hirezero.app` and approved one exact packet to answer “What does HireZero help a user do?” The run completed on the dedicated local WSL host using OpenRouter. The captured page describes a marketing employee that researches, plans and drafts work for owner review, and says hosted signup is paused. The screenshot shows the homepage heading and approval flow.

| Observation | Result |
| --- | --- |
| Browser qualification | Fresh `git archive` of `180aa21`, pinned browser build with network disabled, locked restore, one real-image test passed in 22.238 s, zero skipped. A fresh private qualification receipt binds the image to this engine. |
| Runner | Fresh archive of `51b31fa` with the same worker. The model drafted three steps. Navigate to the homepage, wait for `main`, read `main`. The owner approved the displayed digest before execution. |
| Browser | Completed in 14.429 s. One capture, 5,684 characters and a 59,090-byte JPEG. |
| Network | The host broker recorded ten allowed requests and zero refusals. The browser reported zero blocked off-site origins. This run supplies no live off-site refusal shot. |
| Inference | OpenRouter stand-in, `deepseek/deepseek-v4.1-flash-1017`. Draft took 5.059 s (205 input, 148 output). Answer took 4.027 s (1,800 input, 422 output). Both completed on the first call. Total 2,575 tokens. |
| Release | Confirmed. A separate `docker ps -aq --filter label=io.hirezero.browser.installation` on the same engine returned no IDs, including stopped containers. |

The private evidence includes the qualification receipt, console transcript, itinerary, execution, screenshot, answer, release record, final receipt, and broker and model logs. The exported copies were hash-checked. The [qualification test](../../tests/HireZero.Engine.Tests/BrowserRuntimeTests.cs) and [runner](../../tools/HireZero.Engine.Run/Program.cs) show how to produce the records. A hosted Vultr run remains pending.
