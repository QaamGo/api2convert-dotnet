using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Api2Convert.Exceptions;
using Api2Convert.Http;
using Api2Convert.Models;
using Api2Convert.Support;
using Api2Convert.Upload;

namespace Api2Convert.Resources;

/// <summary>
/// The Jobs resource — full control over the job lifecycle.
///
/// <para>Most users only need <c>client.ConvertAsync()</c>, which is built on top of these methods.
/// Reach for this resource for compound jobs, merges, presets, custom polling or job chaining.</para>
/// </summary>
public sealed class JobsResource
{
    private readonly Transport _transport;
    private readonly FileUploader _uploader;

    public JobsResource(Transport transport, FileUploader uploader)
    {
        _transport = transport;
        _uploader = uploader;
    }

    /// <summary>
    /// Create a job, optionally with an <c>Idempotency-Key</c> that makes the create retry-safe.
    /// Pass <c>"process" =&gt; false</c> to stage it for uploads, then <see cref="StartAsync"/>.
    /// </summary>
    /// <param name="payload">job body: <c>conversion</c>, optional <c>input</c>, <c>process</c>, <c>callback</c>, ...</param>
    /// <param name="idempotencyKey">optional key sent as the <c>Idempotency-Key</c> header (null to omit).</param>
    public async Task<Job> CreateAsync(
        IDictionary<string, object?> payload,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<string, string>? headers = idempotencyKey is not null
            ? new Dictionary<string, string> { ["Idempotency-Key"] = idempotencyKey }
            : null;
        object? result = await _transport
            .RequestAsync("POST", "/jobs", payload, headers: headers, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return Job.FromDict(Data.Object(result));
    }

    public async Task<Job> GetAsync(string jobId, CancellationToken cancellationToken = default)
    {
        object? result = await _transport.RequestAsync("GET", "/jobs/" + UrlPath.Segment(jobId), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return Job.FromDict(Data.Object(result));
    }

    /// <summary>List the current key's jobs (paginated, 50 per page).</summary>
    public async Task<IReadOnlyList<Job>> ListAsync(
        string? status = null,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string> { ["page"] = page.ToString(CultureInfo.InvariantCulture) };
        if (status is not null)
        {
            query["status"] = status;
        }

        object? result = await _transport
            .RequestAsync("GET", "/jobs", query: query, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return Data.MapObjects(result, Job.FromDict);
    }

    /// <summary>Modify a job. The common case — starting a staged job — has the dedicated <see cref="StartAsync"/>.</summary>
    public async Task<Job> UpdateAsync(
        string jobId,
        IDictionary<string, object?> payload,
        CancellationToken cancellationToken = default)
    {
        object? result = await _transport
            .RequestAsync("PATCH", "/jobs/" + UrlPath.Segment(jobId), payload, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return Job.FromDict(Data.Object(result));
    }

    /// <summary>Start processing a staged job (<c>process =&gt; true</c>).</summary>
    public Task<Job> StartAsync(string jobId, CancellationToken cancellationToken = default) =>
        UpdateAsync(jobId, new Dictionary<string, object?> { ["process"] = true }, cancellationToken);

    /// <summary>Cancel a job (whether staged or processing).</summary>
    public Task CancelAsync(string jobId, CancellationToken cancellationToken = default) =>
        _transport.RequestAsync("DELETE", "/jobs/" + UrlPath.Segment(jobId), cancellationToken: cancellationToken);

    /// <summary>
    /// Attach an input by descriptor — e.g. a remote URL:
    /// <c>AddInputAsync(id, new Dictionary&lt;string, object?&gt; { ["type"] = "remote", ["source"] = "https://..." })</c>.
    /// </summary>
    public async Task<InputFile> AddInputAsync(
        string jobId,
        IDictionary<string, object?> input,
        CancellationToken cancellationToken = default)
    {
        object? result = await _transport
            .RequestAsync("POST", "/jobs/" + UrlPath.Segment(jobId) + "/input", input, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return InputFile.FromDict(Data.Object(result));
    }

    /// <summary>
    /// Upload a local file (path <see cref="string"/>, <see cref="System.IO.FileInfo"/>, <c>byte[]</c>
    /// or <see cref="System.IO.Stream"/>) to the job's upload server.
    /// </summary>
    public Task<InputFile> UploadAsync(
        Job job,
        object file,
        string? filename = null,
        CancellationToken cancellationToken = default) =>
        _uploader.UploadAsync(job, file, filename, cancellationToken);

    /// <summary>Outputs produced by the job (use <see cref="GetAsync"/> first, or <see cref="WaitAsync"/>).</summary>
    public async Task<IReadOnlyList<OutputFile>> OutputsAsync(string jobId, CancellationToken cancellationToken = default)
    {
        object? result = await _transport
            .RequestAsync("GET", "/jobs/" + UrlPath.Segment(jobId) + "/output", cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return Data.MapObjects(result, OutputFile.FromDict);
    }

    /// <summary>
    /// Block until the job reaches a terminal status, polling with backoff.
    ///
    /// <para>The interval is floored and the total wait is capped (again — <see cref="Config"/> already
    /// clamps) so no configuration can busy-loop or poll unbounded, and the deadline is a monotonic
    /// wall-clock one (not a sum of sleeps), so slow API responses cannot make the real wait exceed the
    /// timeout.</para>
    /// </summary>
    /// <param name="timeoutSeconds">overrides the configured poll timeout (clamped to a sane maximum).</param>
    /// <param name="throwOnFailure">when true (default), a failed/canceled job throws <see cref="ConversionFailedException"/>.</param>
    /// <exception cref="ConversionFailedException">when the job fails/is canceled and <paramref name="throwOnFailure"/> is true.</exception>
    /// <exception cref="ConversionTimeoutException">when the timeout elapses before completion.</exception>
    public async Task<Job> WaitAsync(
        string jobId,
        int? timeoutSeconds = null,
        bool throwOnFailure = true,
        CancellationToken cancellationToken = default)
    {
        Config config = _transport.Config;

        int timeout = Math.Min(
            Config.MaxPollTimeout,
            Math.Max(0, timeoutSeconds ?? config.PollTimeoutSeconds));
        double maxInterval = Math.Max(Config.MinPollInterval, config.PollMaxInterval);
        double interval = Math.Max(Config.MinPollInterval, config.PollInterval);
        long start = Stopwatch.GetTimestamp();
        var deadline = TimeSpan.FromSeconds(timeout);

        while (true)
        {
            Job job = await GetAsync(jobId, cancellationToken).ConfigureAwait(false);

            if ((job.IsFailed || job.IsCanceled) && throwOnFailure)
            {
                throw new ConversionFailedException(job);
            }

            if (job.IsTerminal)
            {
                return job;
            }

            if (Stopwatch.GetElapsedTime(start) >= deadline)
            {
                throw new ConversionTimeoutException(job, timeout);
            }

            await _transport.PauseAsync(interval, cancellationToken).ConfigureAwait(false);
            interval = Math.Min(maxInterval, interval * 1.5);
        }
    }
}
