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
    public static InputFile FromDict(IReadOnlyDictionary<string, object?> data) =>
        new(
            Data.NullableString(data.GetValueOrDefault("id")),
            Data.String(data.GetValueOrDefault("type")),
            Data.NullableString(data.GetValueOrDefault("source")),
            Data.NullableString(data.GetValueOrDefault("status")),
            Data.NullableString(data.GetValueOrDefault("filename")),
            Data.NullableLong(data.GetValueOrDefault("size")),
            Data.NullableString(data.GetValueOrDefault("content_type")),
            Data.Object(data.GetValueOrDefault("options")));
}
