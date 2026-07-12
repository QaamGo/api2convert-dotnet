using System.Collections.Generic;
using Api2Convert.Support;

namespace Api2Convert.Models;

/// <summary>An input file attached to a job.</summary>
public sealed record InputFile(
    string? Id,
    string Type,
    string? Source,
    string? Status,
    string? Filename,
    long? Size,
    string? ContentType,
    IReadOnlyDictionary<string, object?> Options)
{
    /// <summary>
    /// Locator parameters for a cloud input (<c>bucket</c>, <c>file</c>, <c>host</c>, ...), surfaced on
    /// read. A non-positional init-only property so the record's positional shape is unchanged —
    /// additive, no version bump. Empty for a non-cloud input.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Parameters { get; init; } = new Dictionary<string, object?>(0);

    public static InputFile FromDict(IReadOnlyDictionary<string, object?> data) =>
        new(
            Data.NullableString(data.GetValueOrDefault("id")),
            Data.String(data.GetValueOrDefault("type")),
            Data.NullableString(data.GetValueOrDefault("source")),
            Data.NullableString(data.GetValueOrDefault("status")),
            Data.NullableString(data.GetValueOrDefault("filename")),
            Data.NullableLong(data.GetValueOrDefault("size")),
            Data.NullableString(data.GetValueOrDefault("content_type")),
            Data.Object(data.GetValueOrDefault("options")))
        {
            Parameters = Data.Object(data.GetValueOrDefault("parameters")),
        };
}
