using System.Collections.Generic;
using Api2Convert.Support;

namespace Api2Convert.Models;

/// <summary>
/// A produced output file. <see cref="Uri"/> is a self-contained download URL (no auth), valid for
/// a limited time (24h by default).
/// </summary>
public sealed record OutputFile(
    string? Id,
    string Uri,
    string? Filename,
    long? Size,
    string? Status,
    string? ContentType,
    string? Checksum,
    IReadOnlyDictionary<string, object?> Metadata)
{
    public static OutputFile FromDict(IReadOnlyDictionary<string, object?> data) =>
        new(
            Data.NullableString(data.GetValueOrDefault("id")),
            Data.String(data.GetValueOrDefault("uri")),
            Data.NullableString(data.GetValueOrDefault("filename")),
            Data.NullableLong(data.GetValueOrDefault("size")),
            Data.NullableString(data.GetValueOrDefault("status")),
            Data.NullableString(data.GetValueOrDefault("content_type")),
            Data.NullableString(data.GetValueOrDefault("checksum")),
            Data.Object(data.GetValueOrDefault("metadata")));

    /// <summary>Convenience factory for building an output reference by hand (e.g. from the Jobs API).</summary>
    public static OutputFile Of(string? id, string uri, string? filename) =>
        new(id, uri, filename, null, null, null, null, new Dictionary<string, object?>(0));
}
