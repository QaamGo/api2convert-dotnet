using System.Collections.Generic;
using Api2Convert.Support;

namespace Api2Convert.Models;

/// <summary>A job's status: a machine-readable <see cref="Code"/> plus an optional human <see cref="Info"/>.</summary>
public sealed record Status(string Code, string? Info)
{
    public static Status FromDict(IReadOnlyDictionary<string, object?> data) =>
        new(
            Data.String(data.GetValueOrDefault("code")),
            Data.NullableString(data.GetValueOrDefault("info")));
}
