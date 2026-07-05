using System.Collections.Generic;

namespace Api2Convert.Exceptions;

/// <summary>
/// The API encountered a server-side error (HTTP 5xx). Retried automatically for idempotent
/// requests; thrown once retries are exhausted.
/// </summary>
public sealed class ServerException : ApiException
{
    public ServerException(string message, int statusCode, string? requestId, IReadOnlyDictionary<string, object?>? body)
        : base(message, statusCode, requestId, body)
    {
    }
}
