using System;
using System.Collections.Generic;
using System.Globalization;

namespace Api2Convert.Support;

/// <summary>
/// Typed accessors over a decoded JSON object (a native <see cref="IReadOnlyDictionary{TKey,TValue}"/>
/// produced by <see cref="Json"/>).
///
/// <para>Keeps model hydration null-safe and free of scattered casts. Every accessor tolerates a
/// missing, null or wrong-typed value and returns a default rather than throwing — models must never
/// fail to hydrate on a surprising payload. Internal helper, not part of the public API.</para>
/// </summary>
internal static class Data
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyObject =
        new Dictionary<string, object?>(0);

    private static readonly IReadOnlyList<object?> EmptyList = Array.Empty<object?>();

    /// <summary>Look up a key in a decoded object, or null when absent / not an object.</summary>
    public static object? Get(object? source, string key) =>
        source is IReadOnlyDictionary<string, object?> map && map.TryGetValue(key, out object? value)
            ? value
            : null;

    public static string String(object? value, string defaultValue) =>
        value is string s ? s : defaultValue;

    public static string String(object? value) => String(value, "");

    public static string? NullableString(object? value) => value as string;

    /// <summary>
    /// Coerce a value to an <see cref="int"/>, or null. Accepts JSON numbers and numeric strings;
    /// mirrors PHP <c>is_numeric()</c> by explicitly rejecting booleans (a boolean is not numeric).
    /// </summary>
    public static int? NullableInt(object? value)
    {
        long? l = NullableLong(value);
        if (l is null || l.Value < int.MinValue || l.Value > int.MaxValue)
        {
            // Out of int range: return null (absence) rather than silently wrapping to a garbage value.
            return null;
        }

        return (int)l.Value;
    }

    /// <summary>
    /// Coerce a value to a <see cref="long"/>, or null. Used for byte sizes, which can exceed int.
    /// </summary>
    public static long? NullableLong(object? value)
    {
        switch (value)
        {
            case bool:
                return null;
            case long l:
                return l;
            case int i:
                return i;
            case double d:
                return DoubleToLong(d);
            case string s:
                string t = s.Trim();
                if (long.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed))
                {
                    return parsed;
                }

                if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedDouble))
                {
                    return DoubleToLong(parsedDouble);
                }

                return null;
            default:
                return null;
        }
    }

    /// <summary>
    /// Truncate a double to a long, or null when it is NaN / Infinity or falls outside long range. A
    /// bare <c>(long)d</c> cast is unchecked and yields a garbage value (long.MinValue on overflow),
    /// which would hydrate nonsense instead of signalling absence.
    /// </summary>
    private static long? DoubleToLong(double d)
    {
        // long.MaxValue (2^63-1) is not exactly representable as a double, so use 2^63 as the
        // exclusive upper bound; long.MinValue (-2^63) is exact and allowed.
        if (double.IsNaN(d) || d < long.MinValue || d >= 9223372036854775808.0)
        {
            return null;
        }

        return (long)d;
    }

    public static bool Bool(object? value, bool defaultValue) => value is bool b ? b : defaultValue;

    /// <summary>Return <paramref name="value"/> if it is a JSON object, otherwise an empty map.</summary>
    public static IReadOnlyDictionary<string, object?> Object(object? value) =>
        value as IReadOnlyDictionary<string, object?> ?? EmptyObject;

    /// <summary>
    /// Return <paramref name="value"/> as a list. A JSON array passes through; a JSON object is
    /// reduced to its values (mirroring PHP <c>array_values</c>); anything else is an empty list.
    /// </summary>
    public static IReadOnlyList<object?> List(object? value)
    {
        switch (value)
        {
            case IReadOnlyList<object?> list:
                return list;
            case IReadOnlyDictionary<string, object?> map:
                var values = new List<object?>(map.Count);
                foreach (object? item in map.Values)
                {
                    values.Add(item);
                }

                return values;
            default:
                return EmptyList;
        }
    }

    /// <summary>
    /// Map each JSON-object element of <paramref name="value"/> through <paramref name="factory"/>,
    /// skipping non-objects. The returned list is read-only.
    /// </summary>
    public static IReadOnlyList<T> MapObjects<T>(
        object? value,
        Func<IReadOnlyDictionary<string, object?>, T> factory)
    {
        var output = new List<T>();
        foreach (object? item in List(value))
        {
            if (item is IReadOnlyDictionary<string, object?> map)
            {
                output.Add(factory(map));
            }
        }

        return output;
    }

    public static IReadOnlyList<string> StringList(object? value)
    {
        var output = new List<string>();
        foreach (object? item in List(value))
        {
            if (item is string s)
            {
                output.Add(s);
            }
        }

        return output;
    }
}
