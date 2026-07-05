using System.Collections.Generic;
using Api2Convert.Support;

namespace Api2Convert.Models;

/// <summary>A saved conversion preset (a reusable named set of target + options).</summary>
public sealed record Preset(
    string? Id,
    string Name,
    string? Target,
    string? Category,
    string? Scope,
    IReadOnlyDictionary<string, object?> Options)
{
    public static Preset FromDict(IReadOnlyDictionary<string, object?> data) =>
        new(
            Data.NullableString(data.GetValueOrDefault("id")),
            Data.String(data.GetValueOrDefault("name")),
            Data.NullableString(data.GetValueOrDefault("target")),
            Data.NullableString(data.GetValueOrDefault("category")),
            Data.NullableString(data.GetValueOrDefault("scope")),
            Data.Object(data.GetValueOrDefault("options")));
}
