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
/// API2Convert API end to end. There is exactly one test per documented guide (the same catalog the
/// runnable <c>examples/</c> mirror), plus two negative tests, so this file doubles as an executable
/// tour of the SDK.
///
/// <para>Because these hit the real API and consume quota, every test uses <see cref="LiveFactAttribute"/>,
/// which auto-skips unless <c>API2CONVERT_API_KEY</c> is set — so the default <c>dotnet test</c> run is
/// safe without a key. To run it against a host:</para>
/// <code>
/// API2CONVERT_API_KEY=&lt;behat key&gt; API2CONVERT_BASE_URL=https://api.web8.api2convert.com/v2 \
///     dotnet test test/Api2Convert.LiveTests -c Release
/// </code>
/// <para><c>API2CONVERT_BASE_URL</c> overrides the host (e.g. a beta environment). Never commit a real
/// key — it is read only from the environment.</para>
///
/// <para>The 20 positive scenarios mirror the documented guides shared by every api2convert SDK (php,
/// python, java, go, nodejs, dotnet, ruby, rust). Some operations (video, screenshot, compare,
/// extract) may not be entitled on every key.</para>
/// </summary>
public sealed class ConversionConformanceTests
{
    // Public example files hosted by online-convert.com — the same fixtures used by the docs guides.
    private const string Pdf = "https://example-files.online-convert.com/document/pdf/example.pdf";
    private const string Png = "https://example-files.online-convert.com/raster%20image/png/example.png";
    private const string Jpg = "https://example-files.online-convert.com/raster%20image/jpg/example.jpg";
    private const string JpgSmall = "https://example-files.online-convert.com/raster%20image/jpg/example_small.jpg";
    private const string Wav = "https://example-files.online-convert.com/audio/wav/example.wav";
    private const string Docx = "https://example-files.online-convert.com/document/docx/example.docx";
    private const string Zip = "https://example-files.online-convert.com/archive/zip/example.zip";

    /// <summary>A minimal valid 1×1 PNG, written to disk to exercise the real multipart upload handshake.</summary>
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

    // 1. quickstart — convert a remote JPG to PNG, look the job up, download the output.
    [LiveFact]
    public async Task Quickstart()
    {
        using Api2ConvertClient client = Client();

        ConversionResult result = await client.ConvertAsync(Jpg, "png");
        Assert.True(result.Job.IsCompleted, "job should complete");

        Job job = await client.Jobs.GetAsync(result.Job.Id);
        Assert.True(job.IsCompleted, "fetched job should be completed");

        string dir = ScratchDir("quickstart");
        try
        {
            string path = await result.SaveAsync(dir + Path.DirectorySeparatorChar);
            Assert.True(new FileInfo(path).Length > 0, "output should be non-empty");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // 2. convert-files — list the catalog (all + filtered), then convert JPG -> PNG.
    [LiveFact]
    public async Task ConvertFiles()
    {
        using Api2ConvertClient client = Client();

        IReadOnlyList<IReadOnlyDictionary<string, object?>> all = await client.Conversions.ListAsync();
        Assert.NotEmpty(all);

        IReadOnlyList<IReadOnlyDictionary<string, object?>> toPng =
            await client.Conversions.ListAsync(target: "png");
        Assert.NotEmpty(toPng);

        ConversionResult result = await client.ConvertAsync(Jpg, "png");
        Assert.True(result.Job.IsCompleted, "job should complete");
    }

    // 3. uploading-files — one-call upload + convert of a LOCAL file -> PNG.
    [LiveFact]
    public async Task UploadingFiles()
    {
        using Api2ConvertClient client = Client();

        string dir = ScratchDir("uploading-files");
        string src = Path.Combine(dir, "input.png");
        await File.WriteAllBytesAsync(src, OnePixelPng);
        try
        {
            ConversionResult result = await client.ConvertAsync(src, "png");
            Assert.True(result.Job.IsCompleted, "uploaded job should complete");

            byte[] bytes = await result.ContentsAsync();
            Assert.True(bytes.Length > 0, "converted output should be non-empty");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // 4. job-lifecycle — create (process:false) -> add remote input -> start -> wait -> outputs.
    [LiveFact]
    public async Task JobLifecycle()
    {
        using Api2ConvertClient client = Client();

        Job job = await client.Jobs.CreateAsync(new Dictionary<string, object?>
        {
            ["process"] = false,
            ["conversion"] = new List<object?>
            {
                new Dictionary<string, object?> { ["category"] = "image", ["target"] = "png" },
            },
        });
        Assert.False(string.IsNullOrEmpty(job.Id), "a created job has an id");

        await client.Jobs.AddInputAsync(
            job.Id,
            new Dictionary<string, object?> { ["type"] = "remote", ["source"] = Jpg });
        await client.Jobs.StartAsync(job.Id);

        Job finished = await client.Jobs.WaitAsync(job.Id);
        Assert.True(finished.IsCompleted, "job should complete");

        IReadOnlyList<OutputFile> outputs = await client.Jobs.OutputsAsync(job.Id);
        Assert.NotEmpty(outputs);
    }

    // 5. add-watermark — stamp a PNG onto a PDF (compound job, two remote inputs).
    [LiveFact]
    public async Task AddWatermark()
    {
        using Api2ConvertClient client = Client();

        Job job = await client.Jobs.CreateAsync(new Dictionary<string, object?>
        {
            ["process"] = true,
            ["input"] = new List<object?>
            {
                new Dictionary<string, object?> { ["type"] = "remote", ["source"] = Pdf },
                new Dictionary<string, object?> { ["type"] = "remote", ["source"] = Png },
            },
            ["conversion"] = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    ["category"] = "document",
                    ["target"] = "pdf",
                    ["options"] = new Dictionary<string, object?> { ["stamp"] = true, ["alignment"] = "center" },
                },
            },
        });

        Job finished = await client.Jobs.WaitAsync(job.Id);
        Assert.True(finished.IsCompleted, "job should complete");
        Assert.NotEmpty(finished.Output);
    }

    // 6. create-thumbnails — first page of a PDF -> 300px PNG thumbnail.
    [LiveFact]
    public async Task CreateThumbnails()
    {
        using Api2ConvertClient client = Client();

        ConversionResult result = await client.ConvertAsync(
            Pdf,
            "thumbnail",
            new Dictionary<string, object?>
            {
                ["thumbnail_target"] = "png",
                ["width"] = 300,
                ["pages"] = "first",
                ["dpi"] = 150,
            },
            new ConvertOptions { Category = "operation" });

        Assert.True(result.Job.IsCompleted, "job should complete");
        byte[] bytes = await result.ContentsAsync();
        Assert.True(bytes.Length > 0, "thumbnail should be non-empty");
    }

    // 7. compress-files — compress a JPG with the "compress" operation.
    [LiveFact]
    public async Task CompressFiles()
    {
        using Api2ConvertClient client = Client();

        ConversionResult result = await client.ConvertAsync(
            Jpg,
            "compress",
            new Dictionary<string, object?> { ["compression_level"] = "high" },
            new ConvertOptions { Category = "operation" });

        Assert.True(result.Job.IsCompleted, "job should complete");
        byte[] bytes = await result.ContentsAsync();
        Assert.True(bytes.Length > 0, "compressed output should be non-empty");
    }

    // 8. create-archives — bundle two remote files into a ZIP.
    [LiveFact]
    public async Task CreateArchives()
    {
        using Api2ConvertClient client = Client();

        Job job = await client.Jobs.CreateAsync(new Dictionary<string, object?>
        {
            ["process"] = true,
            ["input"] = new List<object?>
            {
                new Dictionary<string, object?> { ["type"] = "remote", ["source"] = Pdf },
                new Dictionary<string, object?> { ["type"] = "remote", ["source"] = Png },
            },
            ["conversion"] = new List<object?>
            {
                new Dictionary<string, object?> { ["category"] = "archive", ["target"] = "zip" },
            },
        });

        Job finished = await client.Jobs.WaitAsync(job.Id);
        Assert.True(finished.IsCompleted, "job should complete");
        Assert.NotEmpty(finished.Output);
    }

    // 9. create-hashes — SHA-256 of a remote ZIP.
    [LiveFact]
    public async Task CreateHashes()
    {
        using Api2ConvertClient client = Client();

        ConversionResult result = await client.ConvertAsync(
            Zip,
            "sha256",
            options: null,
            opts: new ConvertOptions { Category = "hash" });

        Assert.True(result.Job.IsCompleted, "job should complete");
        byte[] bytes = await result.ContentsAsync();
        Assert.True(bytes.Length > 0, "hash output should be non-empty");
    }

    // 10. extract-assets — extract the embedded assets of a DOCX.
    [LiveFact]
    public async Task ExtractAssets()
    {
        using Api2ConvertClient client = Client();

        ConversionResult result = await client.ConvertAsync(
            Docx,
            "extract-assets",
            options: null,
            opts: new ConvertOptions { Category = "operation" });

        Assert.True(result.Job.IsCompleted, "job should complete");
        Assert.NotEmpty(result.Outputs);
    }

    // 11. file-analysis — read a JPG's metadata as JSON.
    [LiveFact]
    public async Task FileAnalysis()
    {
        using Api2ConvertClient client = Client();

        ConversionResult result = await client.ConvertAsync(
            Jpg,
            "json",
            options: null,
            opts: new ConvertOptions { Category = "metadata" });

        Assert.True(result.Job.IsCompleted, "job should complete");
        byte[] bytes = await result.ContentsAsync();
        Assert.True(bytes.Length > 0, "metadata output should be non-empty");
    }

    // 12. compare-files — SSIM diff of two images.
    [LiveFact]
    public async Task CompareFiles()
    {
        using Api2ConvertClient client = Client();

        Job job = await client.Jobs.CreateAsync(new Dictionary<string, object?>
        {
            ["process"] = true,
            ["input"] = new List<object?>
            {
                new Dictionary<string, object?> { ["type"] = "remote", ["source"] = JpgSmall },
                new Dictionary<string, object?> { ["type"] = "remote", ["source"] = Jpg },
            },
            ["conversion"] = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    ["category"] = "operation",
                    ["target"] = "compare-image",
                    ["options"] = new Dictionary<string, object?>
                    {
                        ["method"] = "ssim",
                        ["threshold"] = 5,
                        ["diff_color"] = "red",
                    },
                },
            },
        });

        Job finished = await client.Jobs.WaitAsync(job.Id);
        Assert.True(finished.IsCompleted, "job should complete");
    }

    // 13. capture-website — screenshot a URL and deliver it as PNG.
    [LiveFact]
    public async Task CaptureWebsite()
    {
        using Api2ConvertClient client = Client();

        Job job = await client.Jobs.CreateAsync(new Dictionary<string, object?>
        {
            ["process"] = true,
            ["input"] = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    ["type"] = "remote",
                    ["source"] = "https://www.online-convert.com",
                    ["engine"] = "screenshot",
                    ["options"] = new Dictionary<string, object?>
                    {
                        ["screen_width"] = 1280,
                        ["screen_height"] = 1024,
                        ["device_scale_factor"] = 1,
                    },
                },
            },
            ["conversion"] = new List<object?>
            {
                new Dictionary<string, object?> { ["category"] = "image", ["target"] = "png" },
            },
        });

        Job finished = await client.Jobs.WaitAsync(job.Id);
        Assert.True(finished.IsCompleted, "job should complete");
        Assert.NotEmpty(finished.Output);
    }

    // 14. audio-operations — re-encode a WAV to stereo AAC at 192 kbps.
    [LiveFact]
    public async Task AudioOperations()
    {
        using Api2ConvertClient client = Client();

        ConversionResult result = await client.ConvertAsync(
            Wav,
            "aac",
            new Dictionary<string, object?>
            {
                ["audio_codec"] = "aac",
                ["audio_bitrate"] = 192,
                ["channels"] = "stereo",
                ["frequency"] = 44100,
            },
            new ConvertOptions { Category = "audio" });

        Assert.True(result.Job.IsCompleted, "job should complete");
        byte[] bytes = await result.ContentsAsync();
        Assert.True(bytes.Length > 0, "audio output should be non-empty");
    }

    // 15. image-operations — resize a JPG to fit 800x600, cropping to keep aspect ratio.
    [LiveFact]
    public async Task ImageOperations()
    {
        using Api2ConvertClient client = Client();

        ConversionResult result = await client.ConvertAsync(
            Jpg,
            "resize-image",
            new Dictionary<string, object?>
            {
                ["width"] = 800,
                ["height"] = 600,
                ["resize_by"] = "px",
                ["resize_handling"] = "keep_aspect_ratio_crop",
            },
            new ConvertOptions { Category = "operation" });

        Assert.True(result.Job.IsCompleted, "job should complete");
        byte[] bytes = await result.ContentsAsync();
        Assert.True(bytes.Length > 0, "resized output should be non-empty");
    }

    // 16. webhooks — start an async DOCX -> PDF conversion with a callback URL; do NOT wait.
    [LiveFact]
    public async Task Webhooks()
    {
        using Api2ConvertClient client = Client();

        Job job = await client.StartConversionAsync(
            Docx,
            "pdf",
            opts: new AsyncOptions
            {
                Category = "document",
                Callback = "https://your-app.example.com/api2convert/webhook",
            });

        // A webhook receipt is not testable in CI; assert only that a started job was returned.
        Assert.False(string.IsNullOrEmpty(job.Id), "async convert returns a started job with an id");
    }

    // 17. presets — list saved presets for a category + target (may be empty).
    [LiveFact]
    public async Task Presets()
    {
        using Api2ConvertClient client = Client();

        IReadOnlyList<Preset> presets = await client.Presets.ListAsync(category: "video", target: "mp4");
        Assert.NotNull(presets);
    }

    // 18. statistics — usage statistics for a recent month.
    [LiveFact]
    public async Task Statistics()
    {
        using Api2ConvertClient client = Client();

        object? stats = await client.Stats.MonthAsync("2026-06");
        Assert.NotNull(stats);
    }

    // 19. rate-limits — read the account's contract information.
    [LiveFact]
    public async Task RateLimits()
    {
        using Api2ConvertClient client = Client();

        object? contracts = await client.Contracts.GetAsync();
        Assert.NotNull(contracts);
    }

    // 20. authentication — a successful authenticated call: list this key's jobs.
    [LiveFact]
    public async Task Authentication()
    {
        using Api2ConvertClient client = Client();

        IReadOnlyList<Job> jobs = await client.Jobs.ListAsync();
        Assert.NotNull(jobs);
    }

    // Negative 1. Validation error on an unknown target -----------------------
    //
    // The API rejects an unknown target — either synchronously at create time (ValidationException)
    // or as a failed job (ConversionFailedException). Both are typed errors you can catch.
    [LiveFact]
    public async Task InvalidTargetIsATypedError()
    {
        using Api2ConvertClient client = Client();

        Api2ConvertException error = await Assert.ThrowsAnyAsync<Api2ConvertException>(
            () => client.ConvertAsync(Jpg, "this-is-not-a-real-target"));
        Assert.True(
            error is ValidationException or ConversionFailedException,
            $"expected a validation/conversion-failed error, got {error.GetType().Name}: {error.Message}");
    }

    // Negative 2. Authentication error, with no secret leak --------------------
    //
    // A bad key produces a typed AuthenticationException carrying the HTTP status (401/403). Crucially,
    // the SDK never puts a credential into an error message — we assert the bogus key does not appear.
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
