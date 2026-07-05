using System.Collections.Generic;

namespace Api2Convert.Exceptions;

/// <summary>
/// Thrown when the API returns an HTTP error response (status &gt;= 400).
///
/// <para>Specific status codes map to dedicated subclasses (<see cref="AuthenticationException"/>,
/// <see cref="ValidationException"/>, <see cref="RateLimitException"/>, <see cref="NotFoundException"/>,
/// <see cref="PaymentRequiredException"/>, <see cref="ServerException"/>); this base type is used for
/// any 4xx that has no more specific subclass.</para>
/// </summary>
public class ApiException : Api2ConvertException
{
    /// <param name="message">error message from the API (the <c>message</c> field) or a fallback.</param>
    /// <param name="statusCode">the HTTP status code of the response.</param>
    /// <param name="requestId">value of the <c>X-Request-Id</c> header, if any (quote it in support requests).</param>
    /// <param name="body">the decoded JSON error body, when available.</param>
    public ApiException(string message, int statusCode, string? requestId, IReadOnlyDictionary<string, object?>? body)
        : base(message)
    {
        StatusCode = statusCode;
        RequestId = requestId;
        Body = body ?? new Dictionary<string, object?>(0);
    }

    /// <summary>The HTTP status code of the error response.</summary>
    public int StatusCode { get; }

    /// <summary>The <c>X-Request-Id</c> header value, if the response carried one. May be null.</summary>
    public string? RequestId { get; }

    /// <summary>The decoded JSON error body (never null; empty when the body was absent or not JSON).</summary>
    public IReadOnlyDictionary<string, object?> Body { get; }
}
