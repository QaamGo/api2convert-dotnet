using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Api2Convert.SecurityTests;

/// <summary>
/// A real loopback HTTP server (backed by <see cref="HttpListener"/>) bound to 127.0.0.1 on a free
/// port. Only a genuine cross-host 302 can prove the transport does not forward an <c>X-Api2convert-*</c>
/// secret header to a redirect target — a mock cannot. Records the headers of every request it
/// receives and counts hits.
/// </summary>
public sealed class LoopbackServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly Action<HttpListenerContext> _handler;
    private readonly object _lock = new();
    private readonly List<IReadOnlyDictionary<string, string>> _headers = new();
    private int _hits;

    private LoopbackServer(Action<HttpListenerContext> handler)
    {
        _handler = handler;
        int port = FreePort();
        BaseUrl = $"http://127.0.0.1:{port}";
        _listener.Prefixes.Add(BaseUrl + "/");
        _listener.Start();
        _ = Task.Run(LoopAsync);
    }

    /// <summary>Base URL of the server, e.g. <c>http://127.0.0.1:54321</c> (no trailing slash).</summary>
    public string BaseUrl { get; }

    /// <summary>How many requests the server has received.</summary>
    public int Hits => Volatile.Read(ref _hits);

    /// <summary>A snapshot of the headers received on each request, in order.</summary>
    public IReadOnlyList<IReadOnlyDictionary<string, string>> HeadersReceived
    {
        get
        {
            lock (_lock)
            {
                return _headers.ToArray();
            }
        }
    }

    /// <summary>A server that answers every request with the given status and body.</summary>
    public static LoopbackServer Responding(int status, string body) =>
        new(ctx => WriteBody(ctx, status, body));

    /// <summary>
    /// A server that drains the request body, waits <paramref name="delay"/>, then responds — used to
    /// prove a streamed transfer is not aborted by the client's whole-request timeout.
    /// </summary>
    public static LoopbackServer RespondingAfterDelay(int status, string body, TimeSpan delay) => new(ctx =>
    {
        using (Stream input = ctx.Request.InputStream)
        {
            input.CopyTo(Stream.Null);
        }

        Thread.Sleep(delay);
        WriteBody(ctx, status, body);
    });

    /// <summary>A server that 302-redirects every request to <paramref name="location"/>.</summary>
    public static LoopbackServer RedirectingTo(string location) => new(ctx =>
    {
        ctx.Response.StatusCode = 302;
        ctx.Response.Headers["Location"] = location;
        ctx.Response.Close();
    });

    public void Dispose()
    {
        try
        {
            _listener.Stop();
            _listener.Close();
        }
        catch (ObjectDisposedException)
        {
            // already closed
        }
    }

    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private static void WriteBody(HttpListenerContext ctx, int status, string body)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(body);
        ctx.Response.StatusCode = status;
        ctx.Response.ContentLength64 = bytes.Length;
        ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
        ctx.Response.OutputStream.Close();
    }

    private async Task LoopAsync()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (Exception e) when (e is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                break;
            }

            Interlocked.Increment(ref _hits);
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string? name in ctx.Request.Headers.AllKeys)
            {
                if (name is not null)
                {
                    headers[name] = ctx.Request.Headers[name] ?? "";
                }
            }

            lock (_lock)
            {
                _headers.Add(headers);
            }

            try
            {
                _handler(ctx);
            }
            catch (Exception e) when (e is HttpListenerException or ObjectDisposedException or System.IO.IOException)
            {
                try
                {
                    ctx.Response.Abort();
                }
                catch (Exception inner) when (inner is ObjectDisposedException or HttpListenerException)
                {
                    // best effort
                }
            }
        }
    }
}
