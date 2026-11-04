# hirezero-engine

Contained runs for AI agents: a browser and a script runtime that each run one bounded task inside a gVisor
container with no network interface, no credentials, a time cap and a memory cap, and are torn down afterwards. The
browser reaches the web only through a host broker that allows approved public HTTPS origins and refuses
everything else.

This engine was lifted from [HireZero](https://hirezero.app/), an AI-company workspace, for the Vultr Agent Rush.
The first commit is that lift; see [What came from HireZero](#what-came-from-hirezero).

## What came from HireZero

Copied from HireZero `main` at `30a8ca9` (Nov 3, 2026), with no behavior changes:

| Path | What it is |
| --- | --- |
| `runtime/browser/`, `runtime/sandbox/` | Pinned worker images (Dockerfile, pin, runner, image check) |
| `runtime/qualification-host/` | Docker and `runsc` (gVisor) settings for the host |
| `src/HireZero.Integrations.Sandbox/` | Docker adapters: container profile, browser and script runs, qualification, recovery |
| `src/HireZero.Integrations.Web/` | `PublicBrowserNetwork`, the allowlisting host broker, plus the public-address and robots.txt checks it uses (`WebReader.cs`, trimmed from HireZero's web reader) |
| `src/HireZero.Core/` | The small contract slice the adapters need (`CompanyJson` copied out of HireZero's `CompanyContracts.cs`) |
| `tests/` | The matching xUnit and Node tests |
| `docs/adr/`, `docs/history/` | The decisions and qualification notes behind the runtimes |

Namespaces keep their HireZero names so the lift stays a straight copy.

## Built Nov 3–8, 2026 (Vultr Agent Rush)

Everything after the first commit was built during the event, on the `vultr-agent-rush` branch.

| Date | What | Where |
| --- | --- | --- |
| Nov 3 | Config-driven model calls: one OpenAI-compatible client, Vultr Serverless Inference by default with DeepSeek V4.1 Flash (`deepseek-v4.1-flash-1017`); OpenRouter only as a named local-test option. No retries, `max_tokens` on every call, token use logged per call without prompts, replies or keys | `src/HireZero.Integrations.Inference/` |
| Nov 3 | Token limits on every call: a per-run limit and a calendar-month limit counted from the usage log, each call reserving its worst case before it is sent; unknown outcomes stay charged in full | `src/HireZero.Integrations.Inference/InferenceBudget.cs` |
| Nov 3 | Every request the browser asks the broker for is reported as allowed or blocked, with why (`off-site`, `address`, `method` and so on), to a JSON Lines log that drops queries and fragments | `src/HireZero.Integrations.Web/BrowserNetworkLog.cs` |
| Nov 3 | Containment tests: a key planted in the host environment never reaches the browser container's arguments or input; every task runs under gVisor with no network, 512 MB, 1 CPU and a two-minute cap, and is removed on release | `tests/HireZero.Engine.Tests/BrowserContainmentTests.cs` |
| Nov 3 | A live smoke test that makes one call and prints its token use; dry run unless `--live` | `tools/HireZero.Engine.Smoke/` |

### Model provider

Every LLM call goes through `InferenceClient`, configured only from environment variables (see
[`.env.example`](.env.example)):

| Variable | Default |
| --- | --- |
| `VULTR_INFERENCE_API_KEY` | required |
| `VULTR_INFERENCE_BASE_URL` | `https://api.vultrinference.com/v1` |
| `HIREZERO_INFERENCE_MODEL` | `deepseek-v4.1-flash-1017` |
| `HIREZERO_INFERENCE_MAX_OUTPUT_TOKENS` | `24000` (DeepSeek reasons before answering and Vultr cannot turn that off) |
| `HIREZERO_INFERENCE_TIMEOUT_SECONDS` | `300` |
| `HIREZERO_INFERENCE_REASONING_EFFORT` | `low` (`minimal`, `low`, `medium` or `high`) |
| `HIREZERO_INFERENCE_RUN_TOKEN_LIMIT` | `200000` tokens per run |
| `HIREZERO_INFERENCE_MONTHLY_TOKEN_LIMIT` | `20000000` tokens per calendar month (UTC), from `receipts/inference-usage.jsonl` |
| `HIREZERO_INFERENCE_PROVIDER` | `vultr`; `openrouter` with `OPENROUTER_API_KEY` for local tests only |

A call is refused before it is sent when its worst case (every input byte a token, 256 tokens of framing, and its
full output limit) would pass either limit. Afterwards it is charged what the provider reported, nothing for a
refusal, or its whole worst case when the outcome is unknown, since an unconfirmed call may still be billed.

```sh
dotnet run --project tools/HireZero.Engine.Smoke            # dry run: shows the provider, sends nothing
dotnet run --project tools/HireZero.Engine.Smoke -- --live  # one call through Vultr, token use to receipts/
```

### Containment

| Rule | How it holds | Proof |
| --- | --- | --- |
| No credentials in the sandbox | The container gets no environment variables, env files, mounts or volumes; model keys live only in the host process | `NoHostCredentialReachesTheBrowserSandbox` |
| Time and memory caps | Each itinerary is capped at 120 seconds; the container gets 512 MB with no swap, 1 CPU and 128 processes | `EveryBrowserTaskIsCappedIsolatedAndTornDown` |
| Teardown after every task | The result is kept, then the container is removed by its exact name, image and labels | same test; a real run is `ThePinnedBrowserRunsBoundedItinerariesAndReleasesItsRealResources` (needs the image) |
| Outbound network restricted | `--network none` under gVisor; the only way out is the host broker, which allows approved public HTTPS origins and logs every blocked request | `BrowserNetworkLogTests` |

The real-image tests still need a qualification run on the Vultr VM.

## Checks

All checks are offline; none calls a model. Tests that need a real container image or Chrome skip unless their opt-in variable is set.

```sh
dotnet test --solution HireZero.Engine.slnx
node --test tests/*.test.mjs
dotnet csharpier check .
```

## License

MIT. See [LICENSE](LICENSE) and [third-party notices](THIRD_PARTY_NOTICES.md).
