using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Api2Convert.Http;

namespace Api2Convert.SecurityTests;

/// <summary>
/// A minimal in-memory <see cref="IHttpSender"/> for the security checks that do not need a real
/// socket (secret-not-in-URL, filename traversal, the URL classifier). Records the requests the SDK
/// sent (so headers/URIs/bodies can be inspected) and serves queued responses in order.
/// </summary>
public sealed class StubSender : IHttpSender
{
    private readonly Queue<(int Status, byte[] Body, bool Json)> _queue = new();

    public List<HttpRequest> Requests { get; } = new();

    public StubSender Json(int status, string json)
    {
        _queue.Enqueue((status, Encoding.UTF8.GetBytes(json), true));
        return this;
    }

    public StubSender Raw(int status, byte[] body)
    {
        _queue.Enqueue((status, body, false));
        return this;
    }

    public Task<HttpResponse> SendAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        if (_queue.Count == 0)
        {
            throw new InvalidOperationException($"StubSender: no response queued for {request.Method} {request.Uri}");
        }

        (int status, byte[] body, bool json) = _queue.Dequeue();
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (json)
        {
            headers["Content-Type"] = "application/json";
        }

        return Task.FromResult(new HttpResponse(status, headers, new MemoryStream(body)));
    }
}
