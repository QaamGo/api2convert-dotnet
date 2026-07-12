using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Api2Convert.Exceptions;
using Api2Convert.Http;
using Api2Convert.Models;
using Api2Convert.Resources;
using Api2Convert.Upload;
using Api2Convert.Webhooks;

namespace Api2Convert;

/// <summary>
/// API2Convert client — convert, compress and transform files with one call.
///
/// <para>Quick start:</para>
/// <code>
/// var client = new Api2ConvertClient("YOUR_API_KEY");
/// var result = await client.ConvertAsync("invoice.docx", "pdf");
/// await result.SaveAsync("invoice.pdf");
/// </code>
///
/// <para><see cref="ConvertAsync"/> hides the multi-step job lifecycle (create → upload → start → poll
/// → download). For full control, use <see cref="Jobs"/> and the other resources.</para>
///
/// <para>Named <c>Api2ConvertClient</c> (the contract's type is <c>Api2Convert</c>) so it does not
/// collide with the <c>Api2Convert</c> namespace — a documented C#-idiom divergence, mirroring Go's
/// <c>Client</c>.</para>
/// </summary>
public sealed class Api2ConvertClient : IDisposable
{
    /// <summary>SDK version, in lockstep with the sibling SDKs. Included in the User-Agent header.</summary>
    public const string Version = "10.2.1";

    private static readonly Regex HttpUrl = new("^https?://", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly Transport _transport;
    private readonly IHttpSender _httpSender;
    private readonly bool _ownsSender;

    /// <summary>Construct with an API key (falls back to the <c>API2CONVERT_API_KEY</c> env var).</summary>
    public Api2ConvertClient(string? apiKey)
        : this(apiKey, null, null, null, null)
    {
    }

    /// <summary>Construct with an API key and tuning <see cref="Config"/>.</summary>
    public Api2ConvertClient(string? apiKey, Config? config)
        : this(apiKey, config, null, null, null)
    {
    }

    /// <summary>Construct with your own <see cref="IHttpSender"/> (e.g. to plug in a different HTTP client).</summary>
    public Api2ConvertClient(string? apiKey, Config? config, IHttpSender? httpSender)
        : this(apiKey, config, httpSender, null, null)
    {
    }

    /// <summary>Full constructor. The <c>sleeper</c> / <c>rng</c> seams are used by tests to make retry/poll waits instant and deterministic.</summary>
    internal Api2ConvertClient(string? apiKey, Config? config, IHttpSender? httpSender, ISleeper? sleeper, Func<double>? rng)
    {
        string key = ResolveKey(apiKey);
        Config cfg = config ?? Config.Default;
        _ownsSender = httpSender is null;
        _httpSender = httpSender ?? new HttpClientSender(cfg.TimeoutSeconds);
        ISleeper slp = sleeper ?? new RealSleeper();
        Func<double> random = rng ?? (() => Random.Shared.NextDouble());

        _transport = new Transport(_httpSender, cfg, key, slp, random);
        var uploader = new FileUploader(_transport);
        Jobs = new JobsResource(_transport, uploader);
        Conversions = new ConversionsResource(_transport);
        Presets = new PresetsResource(_transport);
        Stats = new StatsResource(_transport);
        Contracts = new ContractsResource(_transport);
    }

    /// <summary>Full control over the job lifecycle.</summary>
    public JobsResource Jobs { get; }

    /// <summary>The conversions catalog (supported targets and their options).</summary>
    public ConversionsResource Conversions { get; }

    /// <summary>Saved conversion presets.</summary>
    public PresetsResource Presets { get; }

    /// <summary>API usage statistics.</summary>
    public StatsResource Stats { get; }

    /// <summary>Account contract information.</summary>
    public ContractsResource Contracts { get; }

    /// <summary>
    /// Webhook verifier — usable without a configured client, e.g. in a controller:
    /// <c>Api2ConvertClient.Webhooks().ConstructEvent(rawBody, signatureHeader, secret)</c>.
    /// </summary>
    public static WebhookVerifier Webhooks() => new();

    /// <summary>
    /// Convert a file and wait for the result.
    ///
    /// <para>Hand it a local path, a public URL, or an open stream, name the target format, and get back
    /// a result you can <c>SaveAsync()</c>.</para>
    /// </summary>
    /// <param name="input">a local path <see cref="string"/>, a URL (<c>^https?://</c>), a <see cref="System.IO.FileInfo"/>, a <c>byte[]</c> or a <see cref="System.IO.Stream"/>.</param>
    /// <param name="to">target format, e.g. <c>pdf</c>, <c>jpg</c>, <c>mp4</c>.</param>
    /// <param name="options">target-specific conversion options (discover via <see cref="OptionsAsync"/>); may be null.</param>
    /// <param name="opts">optional less-common controls (category, timeout, output index, ...); may be null.</param>
    /// <param name="cancellationToken">cancels the create/upload/poll/download work.</param>
    public async Task<ConversionResult> ConvertAsync(
        object input,
        string to,
        IDictionary<string, object?>? options = null,
        ConvertOptions? opts = null,
        CancellationToken cancellationToken = default)
    {
        ConvertOptions o = opts ?? new ConvertOptions();
        Job job = await StartConversionInternalAsync(
            input, to, options, o.Category, callback: null, o.Filename, o.DownloadPassword, cancellationToken)
            .ConfigureAwait(false);
        Job done = await Jobs.WaitAsync(job.Id, o.Timeout, throwOnFailure: true, cancellationToken).ConfigureAwait(false);
        return new ConversionResult(done, _transport, o.OutputIndex ?? 0, o.DownloadPassword);
    }

    /// <summary>
    /// Start a conversion without waiting. Pass a <c>Callback</c> URL (via <see cref="AsyncOptions"/>) to
    /// be notified, or poll later with <c>Jobs.GetAsync(job.Id)</c> / <c>Jobs.WaitAsync(job.Id)</c>.
    ///
    /// <para>This is the contract's <c>convertAsync</c>. Renamed <c>StartConversionAsync</c> so the
    /// <c>Async</c> suffix keeps its .NET meaning (returns a <see cref="Task"/>) — a documented idiom
    /// divergence; <see cref="ConvertAsync"/> is the poll-to-completion happy path.</para>
    /// </summary>
    public Task<Job> StartConversionAsync(
        object input,
        string to,
        IDictionary<string, object?>? options = null,
        AsyncOptions? opts = null,
        CancellationToken cancellationToken = default)
    {
        AsyncOptions o = opts ?? new AsyncOptions();
        return StartConversionInternalAsync(
            input, to, options, o.Category, o.Callback, o.Filename, o.DownloadPassword, cancellationToken);
    }

    /// <summary>A <see cref="FileDownload"/> for an output file: <c>await client.Download(out).SaveAsync("./out/")</c>.</summary>
    public FileDownload Download(OutputFile output, string? downloadPassword = null) =>
        new(_transport, output, downloadPassword);

    /// <summary>
    /// Discover the valid options (type / enum / default / range) for a target format:
    /// <c>await client.OptionsAsync("jpg")</c>. Pass <paramref name="category"/> to disambiguate if needed.
    /// </summary>
    public Task<IReadOnlyDictionary<string, object?>> OptionsAsync(
        string target,
        string? category = null,
        CancellationToken cancellationToken = default) =>
        Conversions.OptionsAsync(target, category, cancellationToken);

    public void Dispose()
    {
        if (_ownsSender && _httpSender is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private static string ResolveKey(string? apiKey)
    {
        string? key = !string.IsNullOrEmpty(apiKey) ? apiKey : Environment.GetEnvironmentVariable("API2CONVERT_API_KEY");
        if (string.IsNullOrEmpty(key))
        {
            throw new ConfigurationException(
                "No API key provided. Pass it to the constructor or set the "
                + "API2CONVERT_API_KEY environment variable.");
        }

        return key;
    }

    /// <summary>
    /// Build + start a job from a file/URL/stream input. Shared by <see cref="ConvertAsync"/> and
    /// <see cref="StartConversionAsync"/>: a URL becomes a single remote-input job started immediately;
    /// a local file/stream is staged, uploaded, then started.
    /// </summary>
    private async Task<Job> StartConversionInternalAsync(
        object input,
        string to,
        IDictionary<string, object?>? options,
        string? category,
        string? callback,
        string? filename,
        string? downloadPassword,
        CancellationToken cancellationToken)
    {
        var conversion = new Dictionary<string, object?> { ["target"] = to };
        if (category is not null)
        {
            conversion["category"] = category;
        }

        if (options is { Count: > 0 })
        {
            conversion["options"] = options;
        }

        var job = new Dictionary<string, object?> { ["conversion"] = new List<object?> { conversion } };
        if (callback is not null)
        {
            job["callback"] = callback;
            job["notify_status"] = true;
        }

        if (downloadPassword is not null)
        {
            job["download_passwords"] = new List<object?> { downloadPassword };
        }

        if (input is string source && HttpUrl.IsMatch(source))
        {
            job["process"] = true;
            job["input"] = new List<object?>
            {
                new Dictionary<string, object?> { ["type"] = "remote", ["source"] = source },
            };
            return await Jobs.CreateAsync(job, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        job["process"] = false;
        Job created = await Jobs.CreateAsync(job, cancellationToken: cancellationToken).ConfigureAwait(false);
        await Jobs.UploadAsync(created, input, filename, cancellationToken).ConfigureAwait(false);
        return await Jobs.StartAsync(created.Id, cancellationToken).ConfigureAwait(false);
    }
}
