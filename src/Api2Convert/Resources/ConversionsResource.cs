using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Api2Convert.Http;
using Api2Convert.Support;

namespace Api2Convert.Resources;

/// <summary>
/// The conversions catalog (<c>GET /conversions</c>) — the source of truth for which targets exist
/// and which options each accepts. No authentication needed.
///
/// <para>Use <see cref="OptionsAsync(string, string?, CancellationToken)"/> to discover the valid
/// <c>options</c> for a target before building a conversion.</para>
/// </summary>
public sealed class ConversionsResource
{
    private readonly Transport _transport;

    public ConversionsResource(Transport transport) => _transport = transport;

    /// <summary>
    /// List supported conversions, optionally filtered by category and/or target. Each entry is a
    /// dictionary: <c>{ id, category, target, options }</c>.
    /// </summary>
    public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ListAsync(
        string? category = null,
        string? target = null,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string> { ["page"] = page.ToString(CultureInfo.InvariantCulture) };
        if (category is not null)
        {
            query["category"] = category;
        }

        if (target is not null)
        {
            query["target"] = target;
        }

        object? result = await _transport
            .RequestAsync("GET", "/conversions", query: query, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var rows = new List<IReadOnlyDictionary<string, object?>>();
        foreach (object? row in Data.List(result))
        {
            if (row is IReadOnlyDictionary<string, object?> map)
            {
                rows.Add(map);
            }
        }

        return rows;
    }

    /// <summary>
    /// The option schema (type / enum / default / range) for a single target. <paramref name="category"/>
    /// is optional — pass it only to disambiguate an ambiguous target.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, object?>> OptionsAsync(
        string target,
        string? category = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows =
            await ListAsync(category, target, 1, cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<string, object?> first = rows.Count == 0 ? new Dictionary<string, object?>(0) : rows[0];
        return Data.Object(first.GetValueOrDefault("options"));
    }
}
