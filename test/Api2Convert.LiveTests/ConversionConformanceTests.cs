using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Api2Convert;
using Api2Convert.Exceptions;
using Api2Convert.Http;
using Api2Convert.Models;
using Xunit;

namespace Api2Convert.LiveTests;

/// <summary>
/// Live conformance suite — the canonical, cross-SDK set of scenarios that exercises the real
/// API2Convert API end to end. Every scenario is written to read like a usage example, so this file
/// doubles as an executable tour of the SDK: build a client, convert, discover, drive the job
/// lifecycle, and handle the typed errors.
///
/// <para>Because these hit the real API and consume quota, every test uses <see cref="LiveFactAttribute"/>,
/// which auto-skips unless <c>API2CONVERT_API_KEY</c> is set. So the default <c>dotnet test</c> run is
/// safe without a key. To run it against a host:</para>
/// <code>
/// API2CONVERT_API_KEY=&lt;behat key&gt; API2CONVERT_BASE_URL=https://api.web8.api2convert.com/v2 \
///     dotnet test test/Api2Convert.LiveTests -c Release
/// </code>
/// <para><c>API2CONVERT_BASE_URL</c> overrides the host (e.g. a beta environment). Never commit a real
/// key — it is read only from the environment.</para>
///
/// <para>The seven scenarios mirror the shared spec implemented by every api2convert SDK (php, python,
/// java, go, nodejs, dotnet, ruby, rust):</para>
/// <list type="number">
///   <item><description><see cref="ConvertsRemoteUrlToPng"/> — one-call convert of a URL</description></item>
///   <item><description><see cref="UploadsLocalFileAndConverts"/> — multipart upload of a file</description></item>
///   <item><description><see cref="ConvertsWithOptions"/> — apply conversion options</description></item>
///   <item><description><see cref="DiscoversConversionCatalog"/> — options/catalog discovery</description></item>
///   <item><description><see cref="DrivesJobLifecycleManually"/> — create → input → start → wait</description></item>
///   <item><description><see cref="InvalidTargetIsATypedError"/> — validation error handling</description></item>
///   <item><description><see cref="AuthenticationErrorLeaksNoSecret"/> — auth error, no key leak</description></item>
/// </list>
/// </summary>
public sealed class ConversionConformanceTests
{
    /// <summary>A small, stable public image used as a remote input.</summary>
    private const string RemoteJpg =
        "https://example-files.online-convert.com/raster%20image/jpg/example_small.jpg";

    /// <summary>
    /// A minimal valid 1×1 PNG, written to disk to exercise the real multipart upload handshake
    /// (remote-URL inputs skip upload entirely).
    /// </summary>
    private static readonly byte[] OnePixelPng =
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53,
        0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41, 0x54, 0x08, 0xD7, 0x63, 0xF8, 0xCF, 0xC0, 0x00,
        0x00, 0x00, 0x03, 0x01, 0x01, 0x00, 0x18, 0xDD, 0x8D, 0xB0, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45,
        0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82,
    };

    /// <summary>
    /// The idiomatic client construction: pass the key (here from the environment) and, optionally, a
    /// <see cref="Config"/> pointing at a non-default host so the same suite can target prod or beta.
    /// </summary>
    private static Api2ConvertClient Client()
    {
        string? baseUrl = Environment.GetEnvironmentVariable("API2CONVERT_BASE_URL");
        Config config = string.IsNullOrEmpty(baseUrl)
            ? Config.Default
            : new Config.Builder().BaseUrl(baseUrl).Build();
        return new Api2ConvertClient(Environment.GetEnvironmentVariable("API2CONVERT_API_KEY"), config);
    }

    /// <summary>A per-test scratch directory (tests run in parallel, so each needs its own).</summary>
    private static string ScratchDir(string tag)
    {
        string dir = Path.Combine(
            Path.GetTempPath(),
            $"a2c-live-{Environment.ProcessId}-{tag}-{Path.GetRandomFileName()}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    // 1. One-call convert of a remote URL -------------------------------------
    //
    // The simplest usage: hand ConvertAsync a URL and a target format. The SDK creates a
    // server-side-fetch job, polls it to completion, and hands back a result you can save to disk.
    [LiveFact]
    public async Task ConvertsRemoteUrlToPng()
    {
        using Api2ConvertClient client = Client();

        ConversionResult result = await client.ConvertAsync(RemoteJpg, "png");
        Assert.True(result.Job.IsCompleted, "job should complete");

        string dir = ScratchDir("remote");
        try
        {
            string path = await result.SaveAsync(dir);
            Assert.True(new FileInfo(path).Length > 0, "output should be non-empty");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // 2. Upload and convert a local file --------------------------------------
    //
    // For a local path (or bytes / a stream), the SDK stages the job, streams the file to the per-job
    // upload server (authenticated with the job's token, never your account key), starts it, polls,
    // and downloads.
    [LiveFact]
    public async Task UploadsLocalFileAndConverts()
    {
        using Api2ConvertClient client = Client();

        string dir = ScratchDir("upload");
        string src = Path.Combine(dir, "pixel.png");
        await File.WriteAllBytesAsync(src, OnePixelPng);
        try
        {
            ConversionResult result = await client.ConvertAsync(src, "jpg");
            Assert.True(result.Job.IsCompleted, "uploaded job should complete");

            byte[] bytes = await result.ContentsAsync();
            Assert.True(bytes.Length > 0, "converted output should be non-empty");
            // A JPEG starts with the SOI marker 0xFF 0xD8.
            Assert.Equal(0xFF, bytes[0]);
            Assert.Equal(0xD8, bytes[1]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // 3. Apply conversion options ---------------------------------------------
    //
    // Target-specific options are a plain map, kept strictly separate from the SDK's own controls
    // (ConvertOptions), so an option key can never collide with an SDK argument. Discover the valid
    // keys for a target with client.OptionsAsync (see the next scenario); here we re-encode at a
    // lower JPEG quality.
    [LiveFact]
    public async Task ConvertsWithOptions()
    {
        using Api2ConvertClient client = Client();

        ConversionResult result = await client.ConvertAsync(
            RemoteJpg,
            "jpg",
            // Add e.g. ["width"] = 64, ["height"] = 64 to resize.
            new Dictionary<string, object?> { ["quality"] = 50 });
        Assert.True(result.Job.IsCompleted, "job should complete");

        byte[] bytes = await result.ContentsAsync();
        Assert.True(bytes.Length > 0, "converted output should be non-empty");
    }

    // 4. Discover the conversion catalog --------------------------------------
    //
    // Conversions.ListAsync and OptionsAsync describe what the API can do — which targets exist and
    // which options each accepts. Neither consumes conversion quota, so they are cheap to call before
    // building a request.
    [LiveFact]
    public async Task DiscoversConversionCatalog()
    {
        using Api2ConvertClient client = Client();

        // Which conversions target `jpg`?
        IReadOnlyList<IReadOnlyDictionary<string, object?>> conversions =
            await client.Conversions.ListAsync(target: "jpg");
        Assert.NotEmpty(conversions);

        // The option schema for a target (type / enum / default / range per option).
        IReadOnlyDictionary<string, object?> schema = await client.OptionsAsync("png", "image");
        Assert.NotNull(schema);
    }

    // 5. Drive the full job lifecycle by hand ---------------------------------
    //
    // ConvertAsync is built from these primitives. Driving them yourself unlocks compound/merge jobs,
    // custom inputs, and step-by-step inspection: create a staged job, attach an input, start it,
    // wait for completion, then inspect the job's status and output metadata.
    [LiveFact]
    public async Task DrivesJobLifecycleManually()
    {
        using Api2ConvertClient client = Client();

        // Stage a job (process: false) so we can attach inputs before starting.
        Job job = await client.Jobs.CreateAsync(new Dictionary<string, object?>
        {
            ["process"] = false,
            ["conversion"] = new List<object?>
            {
                new Dictionary<string, object?> { ["target"] = "png" },
            },
        });
        Assert.False(string.IsNullOrEmpty(job.Id), "a created job has an id");

        // Attach a remote input, then start processing.
        await client.Jobs.AddInputAsync(
            job.Id,
            new Dictionary<string, object?> { ["type"] = "remote", ["source"] = RemoteJpg });
        await client.Jobs.StartAsync(job.Id);

        // Poll to a terminal status.
        Job finished = await client.Jobs.WaitAsync(job.Id);
        Assert.True(finished.IsCompleted, "job should complete");

        // Inspect the outputs — both from the finished job and via the outputs API.
        Assert.NotEmpty(finished.Output);
        IReadOnlyList<OutputFile> outputs = await client.Jobs.OutputsAsync(job.Id);
        Assert.Equal(finished.Output.Count, outputs.Count);

        OutputFile output = finished.Output[0];
        Assert.False(string.IsNullOrEmpty(output.Uri), "output has a download URI");
    }

    // 6. Validation error on an unknown target --------------------------------
    //
    // The API rejects an unknown target — either synchronously at create time (ValidationException)
    // or as a failed job (ConversionFailedException). Both are typed errors you can catch.
    [LiveFact]
    public async Task InvalidTargetIsATypedError()
    {
        using Api2ConvertClient client = Client();

        Api2ConvertException error = await Assert.ThrowsAnyAsync<Api2ConvertException>(
            () => client.ConvertAsync(RemoteJpg, "this-is-not-a-real-target"));
        Assert.True(
            error is ValidationException or ConversionFailedException,
            $"expected a validation/conversion-failed error, got {error.GetType().Name}: {error.Message}");
    }

    // 7. Authentication error, with no secret leak ----------------------------
    //
    // A bad key produces a typed AuthenticationException carrying the HTTP status (401/403). Crucially,
    // the SDK never puts a credential into an error message — we assert the bogus key does not appear
    // in the rendered error.
    [LiveFact]
    public async Task AuthenticationErrorLeaksNoSecret()
    {
        // Gate on a real key like the rest of the suite (this only needs the API reachable), then
        // build a SECOND client with a deliberately bogus key.
        using Api2ConvertClient _ = Client();

        const string bogusKey = "a2c-invalid-key-for-testing";
        string? baseUrl = Environment.GetEnvironmentVariable("API2CONVERT_BASE_URL");
        Config config = string.IsNullOrEmpty(baseUrl)
            ? Config.Default
            : new Config.Builder().BaseUrl(baseUrl).Build();
        using var client = new Api2ConvertClient(bogusKey, config);

        AuthenticationException error = await Assert.ThrowsAsync<AuthenticationException>(
            () => client.Jobs.ListAsync());
        Assert.True(
            error.StatusCode is 401 or 403,
            $"expected HTTP 401/403, got {error.StatusCode}");
        Assert.DoesNotContain(bogusKey, error.Message, StringComparison.Ordinal);
    }
}
