# Working on hirezero-engine

- The repository is public (MIT). Never commit keys, tokens, private data, user-profile paths or real receipts.
  Keys come from environment variables; only `.env.example` names them.
- Every model call goes to the configured provider, which defaults to Vultr Serverless Inference. No routine check
  makes a live model call or starts a paid cloud resource.
- Containment is not optional: no credentials in a sandbox, a time cap and a memory cap on every task, teardown after
  every task, and no network except through the host broker.
- An uncertain external outcome is never replayed automatically.
- Use the smallest checks that cover the change. Format before committing (`dotnet csharpier format .`).
