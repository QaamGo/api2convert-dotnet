using System.Collections.Generic;

namespace Api2Convert.Exceptions;

/// <summary>The request was invalid — e.g. an unknown target format or an illegal option (HTTP 400 / 422).</summary>
public sealed class ValidationException : ApiException
{
    public ValidationException(string message, int statusCode, string? requestId, IReadOnlyDictionary<string, object?>? body)
        : base(message, statusCode, requestId, body)
    {
    }
}
