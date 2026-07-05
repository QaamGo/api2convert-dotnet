using System;
using System.Collections.Generic;
using System.IO;

namespace Api2Convert.Http;

/// <summary>
/// A transport-agnostic HTTP request. Either <see cref="Body"/> (a byte array, replayable) or
/// <see cref="StreamBody"/> (a one-shot stream factory, not replayable) is set — never both.
/// </summary>
/// <param name="Method">HTTP method.</param>
/// <param name="Uri">absolute request URI.</param>
/// <param name="Headers">request headers (single-valued).</param>
/// <param name="Body">request body as bytes, or null.</param>
/// <param name="StreamBody">a factory that opens the request body stream (for multipart uploads), or null.</param>
/// <param name="FollowRedirects">
/// whether the sender may follow a redirect for this request. Must be false for any request carrying
/// an auth header (the account key / per-job token / download password travel in custom
/// <c>X-Oc-*</c> headers that a redirect-following client would forward to another host); only the
/// self-contained, no-secret download path sets it true.
/// </param>
/// <param name="Replayable">
/// whether the body can be re-sent from the start (a byte-array/empty body can; a one-shot stream
/// cannot, so it is sent exactly once).
/// </param>
public sealed record HttpRequest(
    string Method,
    string Uri,
    IReadOnlyDictionary<string, string> Headers,
    byte[]? Body,
    Func<Stream>? StreamBody,
    bool FollowRedirects,
    bool Replayable)
{
    /// <summary>A request with a byte-array (or empty) body — replayable.</summary>
    public static HttpRequest Of(
        string method,
        string uri,
        IReadOnlyDictionary<string, string> headers,
        byte[]? body,
        bool followRedirects) =>
        new(method, uri, headers, body, null, followRedirects, true);

    /// <summary>A request with a one-shot streamed body (multipart upload) — not replayable.</summary>
    public static HttpRequest Streaming(
        string method,
        string uri,
        IReadOnlyDictionary<string, string> headers,
        Func<Stream> streamBody) =>
        new(method, uri, headers, null, streamBody, false, false);
}
