using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Api2Convert.Support;

/// <summary>
/// JSON codec over <see cref="System.Text.Json"/>. Decodes into a native CLR object graph
/// (<see cref="Dictionary{TKey,TValue}"/> / <see cref="List{T}"/> / <see cref="string"/> /
/// <see cref="long"/> / <see cref="double"/> / <see cref="bool"/> / null) so model hydration works
/// against plain objects rather than <c>JsonElement</c>, exactly like the sibling SDKs decode into
/// maps/lists. Internal helper, not part of the public API.
/// </summary>
internal static class Json
{
    private static readonly JsonSerializerOptions EncodeOptions = new()
    {
        // Compact output; keys are already the API wire names.
        WriteIndented = false,
    };

    /// <summary>Decode bytes into a native object graph, or null on empty input.</summary>
    public static object? Decode(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            return null;
        }

        using JsonDocument document = JsonDocument.Parse(bytes);
        return Convert(document.RootElement);
    }

    /// <summary>Serialize a native object graph (dictionaries/lists/primitives) to UTF-8 bytes.</summary>
    public static byte[] Encode(object? value) => JsonSerializer.SerializeToUtf8Bytes(value, EncodeOptions);

    private static object? Convert(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                // Ordinal string keys; no prototype/constructor special-casing, so a hostile
                // "__proto__"/"constructor" key is just ordinary data (no pollution analog in .NET).
                var map = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    map[property.Name] = Convert(property.Value);
                }

                return map;

            case JsonValueKind.Array:
                var list = new List<object?>();
                foreach (JsonElement item in element.EnumerateArray())
                {
                    list.Add(Convert(item));
                }

                return list;

            case JsonValueKind.String:
                return element.GetString();

            case JsonValueKind.Number:
                // Prefer an exact 64-bit integer (large file sizes); fall back to double.
                return element.TryGetInt64(out long l) ? l : element.GetDouble();

            case JsonValueKind.True:
                return true;

            case JsonValueKind.False:
                return false;

            default:
                return null;
        }
    }
}
