using System;
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
    /// <summary>
    /// Cloud delivery targets for this conversion's output, if any. A non-positional init-only property
    /// (not a positional record parameter) so existing call sites and the record's binary shape are
    /// unchanged — additive, no version bump. Empty unless the conversion carries an <c>output_target</c>.
    /// </summary>
    public IReadOnlyList<OutputTarget> OutputTargets { get; init; } = Array.Empty<OutputTarget>();

    public static Conversion FromDict(IReadOnlyDictionary<string, object?> data) =>
        new(
            Data.String(data.GetValueOrDefault("target")),
            Data.NullableString(data.GetValueOrDefault("id")),
            Data.NullableString(data.GetValueOrDefault("category")),
            Data.Object(data.GetValueOrDefault("options")),
            Data.Object(data.GetValueOrDefault("metadata")))
        {
            OutputTargets = Data.MapObjects(data.GetValueOrDefault("output_target"), OutputTarget.FromDict),
        };
}
