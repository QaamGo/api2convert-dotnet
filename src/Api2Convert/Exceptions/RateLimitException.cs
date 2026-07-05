using System.Collections.Generic;

namespace Api2Convert.Exceptions;

/// <summary>
/// Too many requests (HTTP 429). The client already retries these automatically with backoff;
/// this is thrown only once retries are exhausted.
/// </summary>
public sealed class RateLimitException : ApiException
{
    /// <param name="retryAfter">seconds to wait before retrying, from the <c>Retry-After</c> header, if provided.</param>
    public RateLimitException(
        string message,
        int statusCode,
        string? requestId,
        IReadOnlyDictionary<string, object?>? body,
        int? retryAfter)
        : base(message, statusCode, requestId, body)
    {
        RetryAfter = retryAfter;
    }

    /// <summary>Seconds to wait before retrying, from <c>Retry-After</c>; may be null.</summary>
    public int? RetryAfter { get; }
}
