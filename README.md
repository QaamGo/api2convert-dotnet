# API2Convert .NET SDK

[![CI](https://github.com/QaamGo/api2convert-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/QaamGo/api2convert-dotnet/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Api2Convert)](https://www.nuget.org/packages/Api2Convert)
![.NET](https://img.shields.io/badge/.NET-%E2%89%A5%208.0-512bd4)
![License](https://img.shields.io/badge/license-MIT-green)

Official .NET / C# SDK for the [API2Convert](https://www.api2convert.com) file-conversion API.
Convert, compress and transform images, documents, audio, video, ebooks, archives and CAD with one
line of code.

- **One-call conversions** — `ConvertAsync(input, to)` hides the create → upload → poll → download
  lifecycle for local files, URLs and streams.
- **Async-first** — every I/O method returns a `Task` and takes a `CancellationToken`.
- **Typed errors** — `AuthenticationException`, `ValidationException`, `RateLimitException`,
  `ConversionFailedException`, … all under one `Api2ConvertException` base.
- **Secure by default** — secrets never appear in logs, URLs or exceptions, and a request carrying a
  secret never follows an HTTP redirect. See [SECURITY.md](SECURITY.md).
- **Zero third-party runtime dependencies** — `HttpClient`, `System.Text.Json` and
  `System.Security.Cryptography` only.

Targets **.NET 8**.

## Install

```sh
dotnet add package Api2Convert
```

## Quick start

```csharp
using Api2Convert;

using var client = new Api2ConvertClient("YOUR_API_KEY"); // or set API2CONVERT_API_KEY

// Convert a local file and save the result.
var result = await client.ConvertAsync("invoice.docx", "pdf");
string path = await result.SaveAsync("invoice.pdf");

// Convert a public URL and read the bytes into memory.
var png = await client.ConvertAsync("https://example.com/photo.jpg", "png");
byte[] bytes = await png.ContentsAsync();
```

The API key is read from the constructor argument, or falls back to the `API2CONVERT_API_KEY`
environment variable.

## Conversion options

Discover the valid options for a target, then pass them to `ConvertAsync`:

```csharp
IReadOnlyDictionary<string, object?> schema = await client.OptionsAsync("jpg");

var result = await client.ConvertAsync(
    "photo.png",
    "jpg",
    new Dictionary<string, object?> { ["quality"] = 80, ["strip"] = true });
```

Less-common controls live on `ConvertOptions`, kept separate from the open-ended options map so they
can never collide with an API option key:

```csharp
var result = await client.ConvertAsync(
    "scan.tiff",
    "pdf",
    options: null,
    opts: new ConvertOptions { Category = "document", DownloadPassword = "hunter2", Timeout = 600 });
```

A download password supplied at conversion time is **remembered** on the result and sent
automatically (as `X-Api2convert-Download-Password`) on every download from it.

## Async (webhook-driven) conversions

`StartConversionAsync` starts a job and returns immediately without polling. Pass a callback URL to be
notified when it finishes (this is the contract's `convertAsync`, renamed so the `Async` suffix keeps
its .NET "returns a `Task`" meaning):

```csharp
var job = await client.StartConversionAsync(
    "https://example.com/big.mov",
    "mp4",
    opts: new AsyncOptions { Callback = "https://app.example.com/hooks/a2c" });

// later — poll it yourself, or handle the webhook (below)
var done = await client.Jobs.WaitAsync(job.Id);
```

## Verifying webhooks

Point a job's callback at your endpoint, then verify the **raw** request body against your signing
secret before trusting it:

```csharp
using Api2Convert;
using Api2Convert.Exceptions;

// framework-agnostic; wire rawBody / signatureHeader to your web request
try
{
    var evt = Api2ConvertClient.Webhooks().ConstructEvent(rawBody, signatureHeader, secret);
    if (evt.Job.IsCompleted)
    {
        foreach (var output in evt.Job.Output)
        {
            // e.g. enqueue a download of output.Uri
        }
    }
}
catch (SignatureVerificationException)
{
    // respond 400 Bad Request — do not trust the payload
}
```

Verification is HMAC-SHA256 over the raw body with a constant-time comparison. An empty secret
deliberately skips verification (use `Webhooks().Parse(rawBody)` when signed webhooks are not enabled
on your account yet).

## Full control: the Jobs API

`ConvertAsync` is built on the resource API, which you can use directly for compound jobs, job
chaining, custom polling or presets:

```csharp
var job = await client.Jobs.CreateAsync(new Dictionary<string, object?>
{
    ["conversion"] = new[] { new Dictionary<string, object?> { ["target"] = "png" } },
    ["process"] = false,
});
await client.Jobs.UploadAsync(job, "input.jpg");
await client.Jobs.StartAsync(job.Id);
var done = await client.Jobs.WaitAsync(job.Id);
```

Resources: `client.Jobs`, `client.Conversions`, `client.Presets`, `client.Stats`, `client.Contracts`.

## Typed errors

```csharp
using Api2Convert.Exceptions;

try
{
    await client.ConvertAsync("photo.jpg", "not-a-format");
}
catch (ValidationException e)   { Console.WriteLine($"Invalid request: {e.Message} (HTTP {e.StatusCode})"); }
catch (RateLimitException e)    { Console.WriteLine($"Slow down; retry after {e.RetryAfter}s"); }
catch (ConversionFailedException e) { Console.WriteLine($"Job {e.Job.Id} failed: {e.Message}"); }
catch (Api2ConvertException e)  { Console.WriteLine($"SDK error: {e.Message}"); } // catch-all base
```

Transient failures (429 / 5xx / network) are retried automatically with capped, jittered exponential
backoff honoring `Retry-After`; a non-idempotent `POST` is never blindly retried, so a transient error
cannot create a duplicate job.

## Configuration

```csharp
var config = new Config.Builder()
    .BaseUrl("https://api.api2convert.com/v2")
    .Timeout(30)          // per-request seconds
    .MaxRetries(2)
    .PollInterval(1.0)    // seconds; floored so it can never busy-loop
    .PollMaxInterval(5.0)
    .PollTimeout(300)     // seconds; capped so it can never poll unbounded
    .Build();

using var client = new Api2ConvertClient("YOUR_API_KEY", config);
```

Plug in your own transport (or a mock) via the `IHttpSender` seam:
`new Api2ConvertClient(apiKey, config, myHttpSender)`.

## Testing

```sh
make check           # build + offline unit suite + independent security suite (no API key)
make test-security   # just the security suite (real loopback servers)

# Live conformance against the real API (auto-skips without a key):
API2CONVERT_API_KEY=<your key> make test-live

# Or run everything in a container on a pinned .NET SDK:
make docker-check
```

The [live conformance suite](test/Api2Convert.LiveTests/ConversionConformanceTests.cs) doubles as an
executable, end-to-end tour of the SDK — there is one test per documented guide (plus two negative
tests), each a self-contained usage example that mirrors the runnable file of the same name in
[`examples/`](examples/).

It runs automatically against the real API on every release tag (see
[`.github/workflows/live-conformance.yml`](.github/workflows/live-conformance.yml)), so a published
version is always verified end to end.

## Examples

Every documented guide has a runnable, self-contained example in
[`examples/Examples`](examples/Examples). They live in one console project; pass the guide slug to run
one (the key is read from `API2CONVERT_API_KEY`, and `API2CONVERT_BASE_URL` retargets the host):

```sh
API2CONVERT_API_KEY=your-key dotnet run --project examples/Examples -- quickstart
```

Run it with no argument to list every guide.

| Guide | File |
|-------|------|
| Quickstart — convert a remote JPG to PNG, then download | [`Quickstart.cs`](examples/Examples/Quickstart.cs) |
| Convert Files — browse the catalog, then convert | [`ConvertFiles.cs`](examples/Examples/ConvertFiles.cs) |
| Uploading Files — one-call upload + convert of a local file | [`UploadingFiles.cs`](examples/Examples/UploadingFiles.cs) |
| Job Lifecycle — create → add input → start → wait → outputs | [`JobLifecycle.cs`](examples/Examples/JobLifecycle.cs) |
| Add a Watermark — stamp a PNG onto a PDF | [`AddWatermark.cs`](examples/Examples/AddWatermark.cs) |
| Create Thumbnails — first PDF page → PNG thumbnail | [`CreateThumbnails.cs`](examples/Examples/CreateThumbnails.cs) |
| Compress Files — shrink a JPG | [`CompressFiles.cs`](examples/Examples/CompressFiles.cs) |
| Create Archives — bundle two files into a ZIP | [`CreateArchives.cs`](examples/Examples/CreateArchives.cs) |
| Create Hashes — SHA-256 of a ZIP | [`CreateHashes.cs`](examples/Examples/CreateHashes.cs) |
| Extract Assets — pull assets out of a DOCX | [`ExtractAssets.cs`](examples/Examples/ExtractAssets.cs) |
| File Analysis — read a JPG's metadata as JSON | [`FileAnalysis.cs`](examples/Examples/FileAnalysis.cs) |
| Compare Files — SSIM diff of two images | [`CompareFiles.cs`](examples/Examples/CompareFiles.cs) |
| Capture a Website — screenshot a URL to PNG | [`CaptureWebsite.cs`](examples/Examples/CaptureWebsite.cs) |
| Audio Operations — re-encode WAV → AAC | [`AudioOperations.cs`](examples/Examples/AudioOperations.cs) |
| Image Operations — resize a JPG | [`ImageOperations.cs`](examples/Examples/ImageOperations.cs) |
| Webhooks — async convert with a callback, and verify a delivery | [`Webhooks.cs`](examples/Examples/Webhooks.cs) |
| Presets — list saved presets | [`Presets.cs`](examples/Examples/Presets.cs) |
| Statistics — usage stats for a month | [`Statistics.cs`](examples/Examples/Statistics.cs) |
| Rate Limits — read contract information | [`RateLimits.cs`](examples/Examples/RateLimits.cs) |
| Authentication — list jobs to confirm the key works | [`Authentication.cs`](examples/Examples/Authentication.cs) |

## License

[MIT](LICENSE) © Qaamgo Media GmbH.
