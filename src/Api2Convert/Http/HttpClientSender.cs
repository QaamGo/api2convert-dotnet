using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Api2Convert.Exceptions;

namespace Api2Convert.Http;

/// <summary>
/// Default <see cref="IHttpSender"/>, backed by <see cref="System.Net.Http.HttpClient"/> (no
/// third-party HTTP dependency).
///
/// <para>Redirect policy is handler-level in .NET, so this holds two clients: one that never follows
/// redirects (used for every request carrying an <c>X-Oc-*</c> secret — the account key / per-job
/// token / download password ride in custom headers that a redirect-following client would forward
/// across hosts) and one that follows normal redirects (used only for the self-contained,
/// no-secret download path, where storage URLs legitimately redirect). The choice is made per
/// request from <see cref="HttpRequest.FollowRedirects"/>.</para>
/// </summary>
public sealed class HttpClientSender : IHttpSender, IDisposable
{
    private static readonly HashSet<string> ContentHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Content-Type",
        "Content-Length",
        "Content-Encoding",
        "Content-Disposition",
        "Content-Language",
    };

    private readonly HttpClient _noRedirect;
    private readonly HttpClient _followRedirects;
    private readonly HttpClient _streaming;

    public HttpClientSender(int timeoutSeconds)
    {
        var timeout = TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds));
        _noRedirect = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false }) { Timeout = timeout };
        _followRedirects = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = true }) { Timeout = timeout };
        // A streamed upload transmits its whole body inside SendAsync, so a fixed whole-request Timeout
        // would abort a large/slow upload once it exceeds TimeoutSeconds (the download body escapes this
        // via ResponseHeadersRead, but the upload body cannot). Give streaming requests a client that
        // bounds only the connect phase (ConnectTimeout) and lets the caller's CancellationToken govern
        // the transfer. Uploads carry a secret (X-Oc-Token), so this client never follows redirects.
        _streaming = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false, ConnectTimeout = timeout })
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
    }

    public async Task<HttpResponse> SendAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        HttpClient client = request.StreamBody is not null
            ? _streaming
            : request.FollowRedirects ? _followRedirects : _noRedirect;

        Uri uri;
        try
        {
            uri = new Uri(request.Uri, UriKind.Absolute);
        }
        catch (Exception e) when (e is UriFormatException or ArgumentException)
        {
            // A malformed URI (e.g. a garbled API-supplied download URL) must surface inside the SDK
            // exception hierarchy, not as a raw parse error. Thrown before the request is sent.
            throw new NetworkException($"Invalid request URI '{request.Uri}': {e.Message}", e);
        }

        using var message = new HttpRequestMessage(new HttpMethod(request.Method), uri);
        HttpContent? content = null;
        if (request.Body is not null)
        {
            content = new ByteArrayContent(request.Body);
        }
        else if (request.StreamBody is not null)
        {
            content = new StreamContent(request.StreamBody());
        }

        if (content is not null)
        {
            // Content-Type / Content-* belong on the content, not the request; everything else
            // (Accept, User-Agent, X-Oc-*, Idempotency-Key) is a request header.
            content.Headers.Clear();
        }

        foreach (KeyValuePair<string, string> header in request.Headers)
        {
            if (ContentHeaders.Contains(header.Key) && content is not null)
            {
                content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            else
            {
                message.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        message.Content = content;

        HttpResponseMessage response;
        try
        {
            response = await client
                .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A genuine caller cancellation propagates unchanged; a timeout (below) does not.
            throw;
        }
        catch (Exception e) when (e is HttpRequestException or IOException or OperationCanceledException)
        {
            // Transport failure (DNS/connection/TLS/read) or per-request timeout — surfaced as a
            // NetworkException so the transport can retry (idempotent requests) or throw.
            throw new NetworkException($"Request to API2Convert failed: {e.Message}", e);
        }

        Stream body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return new HttpResponse((int)response.StatusCode, CollectHeaders(response), body, response);
    }

    public void Dispose()
    {
        _noRedirect.Dispose();
        _followRedirects.Dispose();
        _streaming.Dispose();
    }

    private static IReadOnlyDictionary<string, string> CollectHeaders(HttpResponseMessage response)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, IEnumerable<string>> header in response.Headers)
        {
            AddFirst(headers, header.Key, header.Value);
        }

        foreach (KeyValuePair<string, IEnumerable<string>> header in response.Content.Headers)
        {
            AddFirst(headers, header.Key, header.Value);
        }

        return headers;
    }

    private static void AddFirst(IDictionary<string, string> headers, string name, IEnumerable<string> values)
    {
        if (headers.ContainsKey(name))
        {
            return;
        }

        foreach (string value in values)
        {
            headers[name] = value;
            return;
        }
    }
}
