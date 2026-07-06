using System;

namespace Api2Convert.Support;

/// <summary>
/// Helpers for building request paths from caller-supplied values. Internal helper, not part of the
/// public API.
/// </summary>
internal static class UrlPath
{
    /// <summary>
    /// Percent-encode a caller-supplied value for safe interpolation into a URL path segment.
    /// Without this a value containing <c>/</c>, <c>?</c> or <c>#</c> would alter the request path or
    /// inject a query/fragment. Query parameters are encoded separately (<see cref="Http.Transport.Url"/>).
    /// </summary>
    public static string Segment(string value) => Uri.EscapeDataString(value);
}
