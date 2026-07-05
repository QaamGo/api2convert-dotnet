using System.Collections.Generic;

namespace Api2Convert.Exceptions;

/// <summary>The API key was missing, invalid or not permitted (HTTP 401 / 403).</summary>
public sealed class AuthenticationException : ApiException
{
    public AuthenticationException(string message, int statusCode, string? requestId, IReadOnlyDictionary<string, object?>? body)
        : base(message, statusCode, requestId, body)
    {
    }
}
