# Changelog

All notable changes to this package are documented here. This project adheres to
[Semantic Versioning](https://semver.org/).

## [10.2.0] - 2026-07-04

First public release of the official, hand-written .NET / C# SDK (`Api2Convert` on NuGet), targeting
.NET 8. Behaviour parity with the PHP, Python, Node.js, Go, Ruby and Java SDKs at the same version,
per [`docs/SDK_CONTRACT.md`](SDK_CONTRACT.md).

### Core
- One-call `ConvertAsync(input, to, options?)` happy path that hides the create → upload → poll →
  download lifecycle for local files, URLs and streams; returns a `ConversionResult` with
  `SaveAsync()` / `ContentsAsync()` / `Url`.
- `StartConversionAsync()` for webhook-driven workflows (sets `notify_status` when a callback is given).
- `OptionsAsync(target)` to discover the valid conversion options for a target format.
- Full Jobs API (`client.Jobs`) plus `Conversions`, `Presets`, `Stats` and `Contracts` resources.
- Webhook verification (`Api2ConvertClient.Webhooks()`) with constant-time HMAC-SHA256 over the raw body.

### Reliability & security
- Async-first: every I/O method returns a `Task` and takes a `CancellationToken`.
- Jittered, capped exponential backoff with `Retry-After` support (clamped); a non-idempotent `POST`
  is retried only with an idempotency key, and only replayable bodies are retried.
- Poll interval floored and total wait capped (monotonic deadline) so no configuration can busy-loop
  or poll unbounded.
- Requests carrying a secret (`X-Oc-*`) never follow redirects (no key/token/password leak); only the
  no-secret download path does. Directory downloads and upload filenames sanitized against traversal
  and header injection.
- An independent security suite (`test/Api2Convert.SecurityTests`) proves the redirect and
  secret-hygiene guarantees with real loopback HTTP servers.

### Implementation
- Immutable `record` DTOs with defensive hydration; zero third-party runtime dependencies (JSON via
  `System.Text.Json`, HTTP via `HttpClient`, HMAC via `System.Security.Cryptography`).
- C#-idiom notes: the client type is `Api2ConvertClient`; the contract's non-polling `convertAsync` is
  `StartConversionAsync`; the poll-to-completion method is `Jobs.WaitAsync`; the timeout exception is
  `ConversionTimeoutException` (avoiding `System.TimeoutException`).
