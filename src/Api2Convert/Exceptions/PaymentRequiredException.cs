using System.Collections.Generic;

namespace Api2Convert.Exceptions;

/// <summary>The account has no remaining quota or its contract does not cover the request (HTTP 402).</summary>
public sealed class PaymentRequiredException : ApiException
{
    public PaymentRequiredException(string message, int statusCode, string? requestId, IReadOnlyDictionary<string, object?>? body)
        : base(message, statusCode, requestId, body)
    {
    }
}
