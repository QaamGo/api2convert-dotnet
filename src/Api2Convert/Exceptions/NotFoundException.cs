using System.Collections.Generic;

namespace Api2Convert.Exceptions;

/// <summary>The requested resource does not exist (HTTP 404).</summary>
public sealed class NotFoundException : ApiException
{
    public NotFoundException(string message, int statusCode, string? requestId, IReadOnlyDictionary<string, object?>? body)
        : base(message, statusCode, requestId, body)
    {
    }
}
