using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Api2Convert.Http;

namespace Api2Convert.Tests;

/// <summary>
/// In-memory <see cref="IHttpSender"/> for offline tests — the .NET analog of the sibling SDKs'
/// FakeSender / httpx.MockTransport. Queue canned responses (or a transport exception) in order, then
/// inspect the requests the SDK actually sent. Streaming bodies are materialized so tests can assert
/// on them.
/// </summary>
public sealed class FakeHttpSender : IHttpSender
{
    private readonly Queue<object> _queue = new();

    public List<RecordedRequest> Requests { get; } = new();

    public FakeHttpSender AddJson(int status, string json)
    {
        _queue.Enqueue(new Canned(
            status,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Content-Type"] = "application/json" },
            Encoding.UTF8.GetBytes(json)));
        return this;
    }

    public FakeHttpSender AddJson(int status, string json, IDictionary<string, string> headers)
    {
        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Content-Type"] = "application/json" };
        foreach (KeyValuePair<string, string> header in headers)
        {
            merged[header.Key] = header.Value;
        }

        _queue.Enqueue(new Canned(status, merged, Encoding.UTF8.GetBytes(json)));
        return this;
    }

    public FakeHttpSender AddRaw(int status, byte[] body)
    {
        _queue.Enqueue(new Canned(status, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), body));
        return this;
    }

    public FakeHttpSender AddException(Exception exception)
    {
        _queue.Enqueue(exception);
        return this;
    }

    /// <summary>
    /// Queue a response whose body is an arbitrary (possibly failing) stream. Used to simulate a
    /// mid-download read failure, which a materialized byte[] body cannot.
    /// </summary>
    public FakeHttpSender AddRawStream(int status, Stream body)
    {
        _queue.Enqueue(new CannedStream(status, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), body));
        return this;
    }

    public RecordedRequest At(int index) => Requests[index];

    public RecordedRequest Last() => Requests[^1];

    public async Task<HttpResponse> SendAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        byte[] body;
        if (request.Body is not null)
        {
            body = request.Body;
        }
        else if (request.StreamBody is not null)
        {
            using Stream stream = request.StreamBody();
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            body = buffer.ToArray();
        }
        else
        {
            body = Array.Empty<byte>();
        }

        Requests.Add(new RecordedRequest(
            request.Method,
            request.Uri,
            new Dictionary<string, string>(request.Headers, StringComparer.OrdinalIgnoreCase),
            body,
            request.FollowRedirects));

        if (_queue.Count == 0)
        {
            throw new InvalidOperationException(
                $"FakeHttpSender: no canned response queued for {request.Method} {request.Uri}");
        }

        object next = _queue.Dequeue();
        if (next is Exception exception)
        {
            throw exception;
        }

        if (next is CannedStream cannedStream)
        {
            return new HttpResponse(cannedStream.Status, cannedStream.Headers, cannedStream.Body);
        }

        var canned = (Canned)next;
        return new HttpResponse(canned.Status, canned.Headers, new MemoryStream(canned.Body));
    }

    private sealed record Canned(int Status, IReadOnlyDictionary<string, string> Headers, byte[] Body);

    private sealed record CannedStream(int Status, IReadOnlyDictionary<string, string> Headers, Stream Body);

    /// <summary>A request the SDK sent, with convenient accessors.</summary>
    public sealed class RecordedRequest
    {
        private readonly IReadOnlyDictionary<string, string> _headers;

        public RecordedRequest(
            string method,
            string uri,
            IReadOnlyDictionary<string, string> headers,
            byte[] body,
            bool followRedirects)
        {
            Method = method;
            Uri = uri;
            _headers = headers;
            Body = body;
            FollowRedirects = followRedirects;
        }

        public string Method { get; }

        public string Uri { get; }

        public byte[] Body { get; }

        public bool FollowRedirects { get; }

        public string Header(string name) => _headers.TryGetValue(name, out string? value) ? value : "";

        public string BodyString() => Encoding.UTF8.GetString(Body);

        public IReadOnlyDictionary<string, object?> BodyJson()
        {
            if (Body.Length == 0)
            {
                return new Dictionary<string, object?>(0);
            }

            using JsonDocument document = JsonDocument.Parse(Body);
            return ToDictionary(document.RootElement);
        }

        private static IReadOnlyDictionary<string, object?> ToDictionary(JsonElement element)
        {
            var map = new Dictionary<string, object?>(StringComparer.Ordinal);
            if (element.ValueKind != JsonValueKind.Object)
            {
                return map;
            }

            foreach (JsonProperty property in element.EnumerateObject())
            {
                map[property.Name] = Value(property.Value);
            }

            return map;
        }

        private static object? Value(JsonElement element) => element.ValueKind switch
        {
            JsonValueKind.Object => ToDictionary(element),
            JsonValueKind.Array => ArrayValue(element),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out long l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };

        private static List<object?> ArrayValue(JsonElement element)
        {
            var list = new List<object?>();
            foreach (JsonElement item in element.EnumerateArray())
            {
                list.Add(Value(item));
            }

            return list;
        }
    }
}
