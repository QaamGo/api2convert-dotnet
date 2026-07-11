using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Api2Convert.Exceptions;
using Api2Convert.Support;

namespace Api2Convert.Http;

/// <summary>
/// The HTTP layer: builds authenticated requests, retries transient failures with jittered
/// exponential backoff, maps error responses to typed exceptions and decodes JSON.
///
/// <para>Resources talk to the API through <see cref="RequestAsync"/>; the file uploader and the
/// downloader use <see cref="SendAsync"/> / <see cref="InterpretAsync"/> / <see cref="DownloadAsync"/>
/// directly because they need non-JSON bodies and per-job auth.</para>
/// </summary>
public sealed class Transport
{
    private static readonly HashSet<int> RetryableStatuses = new() { 429, 500, 502, 503, 504 };

    private static readonly HashSet<string> IdempotentMethods =
        new(StringComparer.OrdinalIgnoreCase) { "GET", "HEAD", "PUT", "DELETE", "OPTIONS", "TRACE" };

    private const double MaxBackoffSeconds = 8.0;

    /// <summary>
    /// Upper bound for an honored <c>Retry-After</c>. A server (or misconfigured proxy) asking for an
    /// absurd delay can't stall a worker for hours — we never sleep longer than this per retry.
    /// </summary>
    private const double MaxRetryAfterSeconds = 120.0;

    /// <summary>
    /// Cap on how much of a control-plane (API / error) JSON body the SDK buffers into memory, so a
    /// hostile or buggy server cannot force an unbounded read (OOM) on these paths. File downloads are
    /// streamed to disk and bounded separately.
    /// </summary>
    private const int MaxResponseBytes = 16 * 1024 * 1024; // 16 MiB

    private static readonly string UserAgent =
        $"api2convert-dotnet/{Api2ConvertClient.Version} dotnet/{Environment.Version}";

    private readonly IHttpSender _http;
    private readonly string _apiKey;
    private readonly ISleeper _sleeper;
    private readonly Func<double> _rng;

    public Transport(IHttpSender http, Config config, string apiKey, ISleeper sleeper, Func<double> rng)
    {
        _http = http;
        Config = config;
        _apiKey = apiKey;
        _sleeper = sleeper;
        _rng = rng;
    }

    public Config Config { get; }

    /// <summary>
    /// Sleep for (at least) the given seconds using the configured sleeper. Used by job polling; a
    /// small upward jitter is added so a fleet that starts waiting at the same instant does not poll
    /// in lockstep (thundering herd).
    /// </summary>
    public Task PauseAsync(double seconds, CancellationToken cancellationToken) =>
        _sleeper.SleepAsync(Jitter(seconds), cancellationToken);

    /// <summary>
    /// Perform an authenticated JSON request and return the decoded body (a dictionary, a list, or an
    /// empty dictionary).
    /// </summary>
    public async Task<object?> RequestAsync(
        string method,
        string path,
        IDictionary<string, object?>? body = null,
        IReadOnlyDictionary<string, string>? query = null,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var requestHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["X-Oc-Api-Key"] = _apiKey,
        };
        if (headers is not null)
        {
            foreach (KeyValuePair<string, string> header in headers)
            {
                requestHeaders[header.Key] = header.Value;
            }
        }

        byte[]? bodyBytes = null;
        if (body is not null)
        {
            bodyBytes = Json.Encode(body);
            requestHeaders["Content-Type"] = "application/json";
        }

        HttpRequest request = HttpRequest.Of(method, Url(path, query), requestHeaders, bodyBytes, false);
        using HttpResponse response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        return await InterpretAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Send a fully-built request with retry/backoff. Adds the common Accept and User-Agent headers
    /// but no auth — callers add the header they need.
    /// </summary>
    public async Task<HttpResponse> SendAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var headers = new Dictionary<string, string>(request.Headers, StringComparer.OrdinalIgnoreCase);
        headers.TryAdd("Accept", "application/json");
        headers.TryAdd("User-Agent", UserAgent);
        HttpRequest effective = request with { Headers = headers };

        // A request may be retried only if its body can be replayed from the start. A one-shot stream
        // (a socket/pipe wrapped in a multipart upload) would re-send from an exhausted position,
        // producing a truncated/corrupt request — so it is sent exactly once.
        bool replayable = effective.Replayable;

        // A non-idempotent request (POST /jobs, /jobs/{id}/input, /presets, uploads) must not be
        // auto-retried on a 5xx or network error: the backend may have already acted on the first
        // attempt, so a blind retry would create a duplicate job — and a duplicate charge. Such
        // requests are retried only when they carry an Idempotency-Key. A 429 is safe to retry for any
        // method, since it is rejected before the request is processed.
        bool idempotent = IsIdempotent(effective);

        int attempt = 0;
        while (true)
        {
            HttpResponse response;
            try
            {
                response = await _http.SendAsync(effective, cancellationToken).ConfigureAwait(false);
            }
            catch (NetworkException)
            {
                if (replayable && idempotent && attempt < Config.MaxRetries)
                {
                    await BackoffAsync(attempt, "", cancellationToken).ConfigureAwait(false);
                    attempt++;
                    continue;
                }

                throw;
            }

            int status = response.Status;
            bool mayRetry = RetryableStatuses.Contains(status)
                && replayable
                && attempt < Config.MaxRetries
                && (status == 429 || idempotent);
            if (mayRetry)
            {
                string retryAfter = response.Header("Retry-After");
                response.Dispose();
                await BackoffAsync(attempt, retryAfter, cancellationToken).ConfigureAwait(false);
                attempt++;
                continue;
            }

            return response;
        }
    }

    /// <summary>Throw a typed exception for error responses; otherwise decode the JSON body.</summary>
    public async Task<object?> InterpretAsync(HttpResponse response, CancellationToken cancellationToken)
    {
        await EnsureSuccessfulAsync(response, cancellationToken).ConfigureAwait(false);

        // Every API request rides the no-follow path (secrets travel in X-Oc-* headers), so a 3xx
        // passes EnsureSuccessfulAsync (status < 400) but was deliberately not followed. Decoding its
        // body would yield an empty model; surface it as a typed error instead — mirroring the
        // DownloadAsync guard so an un-followed redirect is never silently swallowed.
        if (response.Status is >= 300 and < 400)
        {
            throw new NetworkException(
                $"API2Convert returned an unexpected redirect (HTTP {response.Status}); the request was not followed.");
        }

        byte[] raw = await ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);
        if (raw.Length == 0)
        {
            return new Dictionary<string, object?>(0);
        }

        object? decoded;
        try
        {
            decoded = Json.Decode(raw);
        }
        catch (JsonException e)
        {
            // A 2xx carrying a non-JSON body (an intermediary HTML/error page slipping through) must
            // still surface as an SDK exception, not a bare parse error outside the documented hierarchy.
            throw new NetworkException($"API2Convert returned a non-JSON success response: {e.Message}", e);
        }

        return decoded is IReadOnlyDictionary<string, object?> or IReadOnlyList<object?>
            ? decoded
            : new Dictionary<string, object?>(0);
    }

    /// <summary>
    /// Throw the appropriate typed exception when <paramref name="response"/> is an HTTP error. On
    /// success the body is left untouched (so a download can stream it).
    /// </summary>
    public async Task EnsureSuccessfulAsync(HttpResponse response, CancellationToken cancellationToken)
    {
        int status = response.Status;
        if (status < 400)
        {
            return;
        }

        IReadOnlyDictionary<string, object?> body =
            DecodeSafe(await ReadBodyAsync(response, cancellationToken).ConfigureAwait(false));
        string message = body.GetValueOrDefault("message") is string s ? s : $"Request failed (HTTP {status})";
        string? requestId = EmptyToNull(response.Header("X-Request-Id"));

        throw status switch
        {
            401 or 403 => new AuthenticationException(message, status, requestId, body),
            402 => new PaymentRequiredException(message, status, requestId, body),
            404 => new NotFoundException(message, status, requestId, body),
            429 => new RateLimitException(message, status, requestId, body, ParseRetryAfter(response.Header("Retry-After"))),
            400 or 422 => new ValidationException(message, status, requestId, body),
            >= 500 => new ServerException(message, status, requestId, body),
            _ => new ApiException(message, status, requestId, body),
        };
    }

    /// <summary>
    /// Download from a (self-contained) URL and return the response whose body stream is the file.
    /// Used for output downloads; the caller owns and disposes the returned <see cref="HttpResponse"/>.
    ///
    /// <para>Redirect-following is enabled ONLY when the request carries no secret. The account key /
    /// token never use this path, but a download password travels in the custom
    /// <c>X-Oc-Download-Password</c> header, and a redirect-following client forwards custom headers
    /// across a cross-host redirect — so a request carrying any <c>X-Oc-*</c> header must not follow
    /// redirects. A plain, passwordless download URL may still redirect (storage/CDN).</para>
    /// </summary>
    public async Task<HttpResponse> DownloadAsync(
        string uri,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken)
    {
        bool carriesSecret = false;
        foreach (string name in headers.Keys)
        {
            if (name.StartsWith("X-Oc-", StringComparison.OrdinalIgnoreCase))
            {
                carriesSecret = true;
                break;
            }
        }

        HttpRequest request = HttpRequest.Of(
            "GET",
            uri,
            new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase),
            null,
            !carriesSecret);

        HttpResponse response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureSuccessfulAsync(response, cancellationToken).ConfigureAwait(false);

            // A 3xx passes EnsureSuccessfulAsync (status < 400), but on the no-follow path (a
            // secret-bearing request) the redirect was deliberately not followed — the body is the
            // redirect page, not the file. Surface it as a NetworkException so a silently-empty or
            // corrupt file never lands on disk instead of the download.
            if (response.Status is >= 300 and < 400)
            {
                throw new NetworkException(
                    "The download did not resolve: a redirect was not followed because the request "
                    + "carried a secret header.");
            }
        }
        catch
        {
            response.Dispose();
            throw;
        }

        return response;
    }

    public string Url(string path, IReadOnlyDictionary<string, string>? query)
    {
        string url = Config.BaseUrl + "/" + (path.StartsWith('/') ? path[1..] : path);
        if (query is null || query.Count == 0)
        {
            return url;
        }

        var sb = new System.Text.StringBuilder(url).Append('?');
        bool first = true;
        foreach (KeyValuePair<string, string> entry in query)
        {
            if (!first)
            {
                sb.Append('&');
            }

            first = false;
            sb.Append(Uri.EscapeDataString(entry.Key)).Append('=').Append(Uri.EscapeDataString(entry.Value));
        }

        return sb.ToString();
    }

    private static async Task<byte[]> ReadBodyAsync(HttpResponse response, CancellationToken cancellationToken)
    {
        try
        {
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            while (true)
            {
                int read = await response.Body.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                // Refuse to buffer past the cap instead of an unbounded CopyToAsync: a control-plane body
                // this large is hostile or buggy, and reading it whole would risk an OOM. We stop at the
                // first over-cap read, so the offending bytes are never fully materialized.
                if (buffer.Length + read > MaxResponseBytes)
                {
                    throw new NetworkException("API response body exceeds 16 MiB.");
                }

                buffer.Write(chunk, 0, read);
            }

            return buffer.ToArray();
        }
        catch (IOException e)
        {
            throw new NetworkException($"Could not read the API response body: {e.Message}", e);
        }
    }

    private static IReadOnlyDictionary<string, object?> DecodeSafe(byte[] raw)
    {
        if (raw.Length == 0)
        {
            return new Dictionary<string, object?>(0);
        }

        try
        {
            return Json.Decode(raw) as IReadOnlyDictionary<string, object?> ?? new Dictionary<string, object?>(0);
        }
        catch (JsonException)
        {
            return new Dictionary<string, object?>(0);
        }
    }

    private async Task BackoffAsync(int attempt, string retryAfterHeader, CancellationToken cancellationToken)
    {
        int? retry = ParseRetryAfter(retryAfterHeader);
        double seconds;
        if (retry is > 0)
        {
            // Honor a positive Retry-After, but never sleep longer than our own ceiling — a huge or
            // hostile value must not stall the caller for hours. Not jittered: the server asked for
            // this exact delay. A zero/past value falls through to the jittered exponential backoff.
            seconds = Math.Min(MaxRetryAfterSeconds, retry.Value);
        }
        else
        {
            seconds = Jitter(Math.Min(MaxBackoffSeconds, 0.5 * Math.Pow(2, attempt)));
        }

        await _sleeper.SleepAsync(seconds, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Parse a <c>Retry-After</c> header into whole seconds. Supports the delay-seconds form
    /// (<c>120</c>) and the HTTP-date form (<c>Wed, 21 Oct 2015 07:28:00 GMT</c>). Returns null when
    /// absent/unparseable; never negative.
    /// </summary>
    private static int? ParseRetryAfter(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        string trimmed = value.Trim();
        if (long.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out long seconds))
        {
            return (int)Math.Max(0, seconds);
        }

        if (DateTimeOffset.TryParse(
                trimmed,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset when))
        {
            long delta = (long)(when - DateTimeOffset.UtcNow).TotalSeconds;
            return (int)Math.Max(0, delta);
        }

        return null;
    }

    /// <summary>
    /// Add a small upward jitter (0-25%) so correlated clients don't retry/poll in lockstep.
    /// Upward-only, so a jittered delay is never shorter than requested.
    /// </summary>
    private double Jitter(double seconds) => seconds + (seconds * 0.25 * _rng());

    /// <summary>
    /// Whether a request is safe to auto-retry after a 5xx or network failure. GET/HEAD/PUT/DELETE/
    /// OPTIONS/TRACE are idempotent by HTTP semantics; a request of any method carrying an
    /// <c>Idempotency-Key</c> is retry-safe too. Everything else — notably a bare POST — is not.
    /// </summary>
    private static bool IsIdempotent(HttpRequest request)
    {
        if (IdempotentMethods.Contains(request.Method))
        {
            return true;
        }

        return request.Headers.TryGetValue("Idempotency-Key", out string? key) && !string.IsNullOrEmpty(key);
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
