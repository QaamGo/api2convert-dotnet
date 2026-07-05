using System;
using System.Collections.Generic;
using System.Text.Json;
using Api2Convert.Http;

namespace Api2Convert.Tests;

/// <summary>
/// Base test class: builds an <see cref="Api2ConvertClient"/> backed by an in-memory
/// <see cref="FakeHttpSender"/>, so tests never touch the network, and a recording sleeper + zero-jitter
/// RNG so retry/poll waits are instant, deterministic and assertable.
/// </summary>
public abstract class A2CTestBase
{
    protected FakeHttpSender Http { get; } = new();

    protected RecordingSleeper Sleeper { get; } = new();

    protected Api2ConvertClient Client(Config? config = null) =>
        new("test-key", config ?? Config.Default, Http, Sleeper, () => 0.0);

    protected FakeHttpSender.RecordedRequest RequestAt(int index) => Http.At(index);

    protected static IReadOnlyDictionary<string, object?> Parse(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        var map = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (JsonProperty property in document.RootElement.EnumerateObject())
        {
            map[property.Name] = Convert(property.Value);
        }

        return map;
    }

    private static object? Convert(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => ObjectValue(element),
        JsonValueKind.Array => ArrayValue(element),
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out long l) ? l : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null,
    };

    private static Dictionary<string, object?> ObjectValue(JsonElement element)
    {
        var map = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            map[property.Name] = Convert(property.Value);
        }

        return map;
    }

    private static List<object?> ArrayValue(JsonElement element)
    {
        var list = new List<object?>();
        foreach (JsonElement item in element.EnumerateArray())
        {
            list.Add(Convert(item));
        }

        return list;
    }
}
