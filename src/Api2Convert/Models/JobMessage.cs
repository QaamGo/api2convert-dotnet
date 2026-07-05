using System.Collections.Generic;
using Api2Convert.Support;

namespace Api2Convert.Models;

/// <summary>An error or warning attached to a job (the <c>errors[]</c> / <c>warnings[]</c> entries).</summary>
public sealed record JobMessage(
    int? Code,
    string Message,
    string? Source,
    string? IdSource,
    IReadOnlyDictionary<string, object?> Details)
{
    public static JobMessage FromDict(IReadOnlyDictionary<string, object?> data) =>
        new(
            Data.NullableInt(data.GetValueOrDefault("code")),
            Data.String(data.GetValueOrDefault("message")),
            Data.NullableString(data.GetValueOrDefault("source")),
            Data.NullableString(data.GetValueOrDefault("id_source")),
            Data.Object(data.GetValueOrDefault("details")));
}
