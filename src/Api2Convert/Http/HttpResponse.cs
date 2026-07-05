using System;
using System.Collections.Generic;
using System.IO;

namespace Api2Convert.Http;

/// <summary>
/// A transport-agnostic HTTP response. The body is exposed as a stream: callers that need the whole
/// body (JSON) read it fully; the download path consumes it in chunks. The body is read at most once
/// and the caller owns it — disposing the response disposes the body (and releases the connection).
/// </summary>
public sealed class HttpResponse : IDisposable
{
    private readonly IReadOnlyDictionary<string, string> _headers;
    private readonly IDisposable? _owner;

    /// <param name="status">the HTTP status code.</param>
    /// <param name="headers">first value per header name; comparisons are case-insensitive.</param>
    /// <param name="body">the response body stream (never null; empty stream when there is no body).</param>
    /// <param name="owner">an optional resource disposed together with this response (e.g. the underlying message).</param>
    public HttpResponse(
        int status,
        IReadOnlyDictionary<string, string> headers,
        Stream body,
        IDisposable? owner = null)
    {
        Status = status;
        _headers = headers;
        Body = body;
        _owner = owner;
    }

    /// <summary>The HTTP status code.</summary>
    public int Status { get; }

    /// <summary>The response body stream. The caller owns it and must dispose it (or this response).</summary>
    public Stream Body { get; }

    /// <summary>First value of the given header, or <c>""</c> if absent. Case-insensitive.</summary>
    public string Header(string name) =>
        _headers.TryGetValue(name, out string? value) ? value : "";

    public void Dispose()
    {
        Body.Dispose();
        _owner?.Dispose();
    }
}
