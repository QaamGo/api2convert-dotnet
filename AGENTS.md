# AGENTS — maintaining the API2Convert .NET SDK

This SDK is **hand-written** (not generated from OpenAPI) and kept in sync with the API by a human
**or an AI agent**. It is one of the official ports (PHP, Python, Java, Node.js, Go, Ruby, .NET) that
all implement the same language-agnostic contract in [`docs/SDK_CONTRACT.md`](docs/SDK_CONTRACT.md).

## Why hand-written

The conversion flow is multi-step (create → upload → poll → download) and the **upload step is not in
the OpenAPI spec at all**, so a generator cannot produce a usable client. We optimise for a
junior-friendly surface — one-call `ConvertAsync()` — and use AI to keep it current.

## Repo layout

| Path | What it is |
| --- | --- |
| `src/Api2Convert/Api2ConvertClient.cs`, `ConvertOptions.cs`, `AsyncOptions.cs` | The client + `ConvertAsync` / `StartConversionAsync` / `Download` façade and per-call options. **Hand-authored.** |
| `src/Api2Convert/ConversionResult.cs`, `FileDownload.cs` | Result + download helpers. **Hand-authored.** |
| `src/Api2Convert/Upload/FileUploader.cs` | Streaming multipart upload to the per-job server. **Hand-authored** (not in the spec). |
| `src/Api2Convert/Webhooks/` | Webhook HMAC verification + parsing. **Hand-authored.** |
| `src/Api2Convert/Resources/` | One type per API tag (Jobs, Conversions, Presets, Stats, Contracts). **Derived** from the spec. |
| `src/Api2Convert/Models/`, `Enums/` | Immutable `record` models (`FromDict` factories) / enums. **Derived** from the spec. |
| `src/Api2Convert/Http/` | Transport: auth, retries/backoff, error mapping, redirect policy, the `IHttpSender` seam. |
| `src/Api2Convert/Exceptions/` | The typed exception hierarchy. |
| `src/Api2Convert/Support/` | `Data` (tolerant JSON hydration) + `Json` (System.Text.Json codec). |
| `openapi/api2convert.openapi.json` | **Committed spec snapshot** — the diff baseline (keep md5-identical to siblings). |
| `docs/SDK_CONTRACT.md` | The fixed, language-agnostic public surface + semantics (keep md5-identical to siblings). |
| `test/Api2Convert.Tests/` | Offline unit tests (fake `IHttpSender`). **The guardrail.** |
| `test/Api2Convert.SecurityTests/` | The independent security suite (real loopback servers). **The redirect/leak guardrail.** |
| `test/Api2Convert.LiveTests/` | Live conformance (auto-skips without `API2CONVERT_API_KEY`). |

## How to update the SDK to a new API version

1. **Refresh the snapshot.** Overwrite `openapi/api2convert.openapi.json` from
   `https://api.api2convert.com/v2/openapi.json` and `git diff` it. Keep it md5-identical to siblings.
2. **Diff it** — new/removed/renamed operations, new fields, new enum values.
3. **Update the DERIVED layer to match the diff, and nothing else:**
   - New/changed fields → update the relevant record in `Models/` + its `FromDict`.
   - New operation → add a method on the matching resource in `Resources/` (mirror the style).
   - New input/output target types → extend `Enums/`.
4. **Do NOT change the hand-authored public API** (`ConvertAsync`, `StartConversionAsync`, `Download`,
   upload, polling, webhook verification, exception types) unless `docs/SDK_CONTRACT.md` changes first.
   If a real product change requires it, update the contract in the same change and bump the **major**
   version.
5. **Build and test (the guardrail):**
   ```sh
   make check     # build + offline unit tests + security suite — all must pass
   ```
   Add or update a test for any new behavior. Keep the live conformance test runnable.
6. **Record + version.** Add a `docs/CHANGELOG.md` entry and bump `<Version>` in
   `src/Api2Convert/Api2Convert.csproj` **and** the `Version` constant in `Api2ConvertClient.cs` per
   SemVer (additive spec change → minor; breaking public-surface change → major). Tag `vX.Y.Z`.

## Guarantees to uphold (don't break these)

- **Never commit a real API key, token or secret** — not in source, tests, fixtures, examples, CI
  files or commit messages. Keys come only from environment variables (`API2CONVERT_API_KEY`) or
  masked/protected CI variables; tests use obvious fakes (`test-key`, `whsec_test`, …). The live suite
  reads the key from the environment at runtime — the behat default key is exported into
  `API2CONVERT_API_KEY`, never written to a file. The SDK must never log or expose a key/token in
  errors. Secret-scan before any release.
- **The contract is law.** Public method names, signatures and semantics match `docs/SDK_CONTRACT.md`
  across every SDK language, adapted only to C# idiom (see divergences below).
- **Upload uses the per-job `X-Oc-Token`, never the account key.** There is a test for this.
- **Secret-bearing requests never follow redirects.** The key/token/download-password ride in custom
  `X-Oc-*` headers that a redirect-following client would forward across hosts. Only the no-secret
  download path follows redirects (a second `HttpClientHandler`). `Api2Convert.SecurityTests` proves
  the guarantee with real servers.
- **`ConvertAsync()` stays one call** for the common case (path/URL/stream → `to` → `SaveAsync()`).
- **Transient failures retry; failures surface as typed exceptions.** Never leak a raw transport error
  (wrap it in `NetworkException`). A non-idempotent `POST` is never blindly retried.
- **.NET 8, zero runtime dependencies (BCL only).** Don't add a runtime `PackageReference`; JSON is
  `System.Text.Json`, HTTP is `HttpClient`, HMAC is `System.Security.Cryptography`.

## C#-idiom divergences from the contract

The contract fixes names and semantics; these are the only places .NET deviates, all for idiom:

- **Async-first.** Every I/O method is a `Task`-returning `…Async` method taking a `CancellationToken`
  (`ConvertAsync`, `Jobs.WaitAsync`, `SaveAsync`, …). `Download()` and option construction do no I/O
  (no `Async`) until `SaveAsync`/`ContentsAsync`.
- **`convertAsync` → `StartConversionAsync`.** The contract's non-polling `convertAsync` (start a job,
  return the `Job`) is renamed so the `Async` suffix keeps its .NET meaning ("returns a `Task`"); the
  poll-to-completion happy path is `ConvertAsync` (the contract's `convert`). Mirrors Go naming its
  client `Client`.
- **`Api2Convert` → `Api2ConvertClient`.** The type is named `Api2ConvertClient` so it does not collide
  with the `Api2Convert` namespace.
- **`TimeoutException` → `ConversionTimeoutException`.** Avoids colliding with `System.TimeoutException`.
- **Resource accessors are properties** (`client.Jobs`, `client.Conversions`, …), the C# convention,
  not methods.
- **Per-call controls are object-initializer options** (`new ConvertOptions { Category = … }`) rather
  than fluent builders, kept separate from the open-ended conversion-options map as the contract
  requires. The client-construction knobs use a `Config.Builder`.
- **Models are `record`s** with `FromDict` factories; nullable numbers are `long?`/`int?`, nullable
  strings are `string?`. `Job.Raw` keeps the full response. Status predicates are properties
  (`IsCompleted`, `IsFailed`, `IsCanceled`, `IsTerminal`).
- **The security suite is a separate black-box project** (`test/Api2Convert.SecurityTests`) runnable in
  isolation — the .NET analog of the siblings' isolated security suites.

## Conventions

- Models parse defensively via `Support/Data.cs` (tolerate missing/extra/wrong-typed fields; never
  throw during hydration). `Job.Raw` keeps the full response.
- Resource methods are thin: build the request, call the transport, hydrate a model.
- Keep the README quickstart copy-pasteable; if you change the happy path, update the README example.
