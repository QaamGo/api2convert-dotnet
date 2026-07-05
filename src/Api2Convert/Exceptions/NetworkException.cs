using System;

namespace Api2Convert.Exceptions;

/// <summary>
/// A request did not yield a usable response. Two throw sites:
///
/// <list type="bullet">
///   <item>a transport-level failure (DNS, connection, TLS or read failure) — retried automatically
///       for idempotent requests and thrown once retries are exhausted;</item>
///   <item>a successful (2xx) response whose body is not valid JSON (e.g. an intermediary HTML/error
///       page) — thrown directly by the decoder and not retried.</item>
/// </list>
/// </summary>
public sealed class NetworkException : Api2ConvertException
{
    public NetworkException(string message)
        : base(message)
    {
    }

    public NetworkException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
