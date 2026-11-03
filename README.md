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

## Checks

All checks are offline. Tests that need a real container image or Chrome skip unless their opt-in variable is set.

```sh
dotnet test --solution HireZero.Engine.slnx
node --test tests/*.test.mjs
dotnet csharpier check .
```

## License

MIT. See [LICENSE](LICENSE) and [third-party notices](THIRD_PARTY_NOTICES.md).
