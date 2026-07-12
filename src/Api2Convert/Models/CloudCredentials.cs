using System.Collections.Generic;
using Api2Convert.Support;

namespace Api2Convert.Models;

/// <summary>
/// An opaque wrapper around a cloud descriptor's secret <c>credentials</c> map (access keys,
/// passwords, tokens).
///
/// <para>Credentials travel in the plaintext request body, so this type's <see cref="ToString"/> always
/// renders the fixed <c>[REDACTED]</c> marker — never the underlying values. That makes the whole
/// object safe wherever an inspection path could reach it (a log line, a record's default rendering, a
/// debugger). The raw map is still available via <see cref="Values"/> for wire serialization only.</para>
/// </summary>
public sealed class CloudCredentials
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyMap = new Dictionary<string, object?>(0);

    /// <summary>An empty credentials object (what a read model surfaces — the API never returns values).</summary>
    public static readonly CloudCredentials Empty = new(null);

    private readonly IReadOnlyDictionary<string, object?> _values;

    public CloudCredentials(IReadOnlyDictionary<string, object?>? values) => _values = values ?? EmptyMap;

    /// <summary>The raw credential map — used for wire serialization only; never render it.</summary>
    public IReadOnlyDictionary<string, object?> Values => _values;

    /// <summary>Number of credential entries (0 for an empty/read-surfaced object).</summary>
    public int Count => _values.Count;

    /// <summary>Always the fixed marker — the credential values never render.</summary>
    public override string ToString() => Redactor.Marker;

    public override bool Equals(object? obj)
    {
        if (obj is not CloudCredentials other || _values.Count != other._values.Count)
        {
            return false;
        }

        foreach (KeyValuePair<string, object?> entry in _values)
        {
            if (!other._values.TryGetValue(entry.Key, out object? value) || !Equals(entry.Value, value))
            {
                return false;
            }
        }

        return true;
    }

    public override int GetHashCode() => _values.Count;
}
