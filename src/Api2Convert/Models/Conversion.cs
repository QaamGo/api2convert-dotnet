using System.Collections.Generic;
using Api2Convert.Support;

namespace Api2Convert.Models;

/// <summary>A single conversion within a job: the target format plus its options.</summary>
public sealed record Conversion(
    string Target,
    string? Id,
    string? Category,
    IReadOnlyDictionary<string, object?> Options,
    IReadOnlyDictionary<string, object?> Metadata)
{
    public static Conversion FromDict(IReadOnlyDictionary<string, object?> data) =>
        new(
            Data.String(data.GetValueOrDefault("target")),
            Data.NullableString(data.GetValueOrDefault("id")),
            Data.NullableString(data.GetValueOrDefault("category")),
            Data.Object(data.GetValueOrDefault("options")),
            Data.Object(data.GetValueOrDefault("metadata")));
}
