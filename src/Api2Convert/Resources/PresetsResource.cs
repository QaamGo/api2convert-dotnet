using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Api2Convert.Http;
using Api2Convert.Models;
using Api2Convert.Support;

namespace Api2Convert.Resources;

/// <summary>Saved conversion presets (reusable named target + options).</summary>
public sealed class PresetsResource
{
    private readonly Transport _transport;

    public PresetsResource(Transport transport) => _transport = transport;

    public async Task<IReadOnlyList<Preset>> ListAsync(
        string? category = null,
        string? target = null,
        string? filter = null,
        CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string>();
        if (category is not null)
        {
            query["category"] = category;
        }

        if (target is not null)
        {
            query["target"] = target;
        }

        if (filter is not null)
        {
            query["filter"] = filter;
        }

        object? result = await _transport
            .RequestAsync("GET", "/presets", query: query, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return Data.MapObjects(result, Preset.FromDict);
    }

    /// <param name="payload"><c>{ name, target, options, scope?, category? }</c></param>
    public async Task<Preset> CreateAsync(IDictionary<string, object?> payload, CancellationToken cancellationToken = default)
    {
        object? result = await _transport.RequestAsync("POST", "/presets", payload, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return Preset.FromDict(Data.Object(result));
    }

    public async Task<Preset> GetAsync(string presetId, CancellationToken cancellationToken = default)
    {
        object? result = await _transport.RequestAsync("GET", "/presets/" + UrlPath.Segment(presetId), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return Preset.FromDict(Data.Object(result));
    }

    public async Task<Preset> UpdateAsync(
        string presetId,
        IDictionary<string, object?> payload,
        CancellationToken cancellationToken = default)
    {
        object? result = await _transport
            .RequestAsync("PATCH", "/presets/" + UrlPath.Segment(presetId), payload, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return Preset.FromDict(Data.Object(result));
    }

    public Task DeleteAsync(string presetId, CancellationToken cancellationToken = default) =>
        _transport.RequestAsync("DELETE", "/presets/" + UrlPath.Segment(presetId), cancellationToken: cancellationToken);
}
