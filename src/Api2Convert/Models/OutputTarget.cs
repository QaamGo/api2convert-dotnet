using System.Collections.Generic;
using Api2Convert.Enums;
using Api2Convert.Support;

namespace Api2Convert.Models;

/// <summary>
/// A cloud-storage delivery target for a conversion's output:
/// <c>{ type:&lt;provider&gt;, parameters, credentials }</c>.
///
/// <para>Attach one (or more) to a conversion via <c>client.ConvertAsync(..., new ConvertOptions
/// { OutputTargets = [...] })</c> / <c>StartConversionAsync(...)</c>, or inline in a raw
/// <c>Jobs.CreateAsync</c> conversion map. When any output target is set the conversion delivers straight
/// to your storage and produces <strong>no</strong> local output — so <c>ConvertAsync</c> returns the
/// completed job without downloading.</para>
///
/// <para>This wave ships the <strong>generic</strong> shape only (<c>type</c> + free-form
/// <c>parameters</c> / <c>credentials</c>); the per-provider output keys live in a separate service and
/// diverge per provider, so there are no per-provider output factories yet.</para>
///
/// <para>Serialization (<see cref="ToDescriptor"/>) emits <c>{ type, parameters, credentials }</c> and
/// <strong>omits <c>status</c></strong> (server-set, read-only). On read (<see cref="FromDict"/>)
/// <c>type</c>, <c>parameters</c> and <c>status</c> round-trip as raw values; <c>credentials</c> are
/// <strong>never</strong> surfaced (the API returns them empty). <c>credentials</c> ride in the plaintext
/// body, so <see cref="ToString"/> masks the whole object to <c>[REDACTED]</c>.</para>
/// </summary>
public sealed record OutputTarget
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyMap = new Dictionary<string, object?>(0);

    /// <param name="type">the delivery provider, as a raw wire string (or via <see cref="Of(CloudProvider, IReadOnlyDictionary{string, object?}, IReadOnlyDictionary{string, object?})"/>).</param>
    /// <param name="parameters">delivery locator keys (provider-specific).</param>
    /// <param name="credentials">secret keys (never surfaced on read).</param>
    /// <param name="status">server-set delivery status on read (<c>waiting|uploading|completed|failed</c>); never sent on create.</param>
    public OutputTarget(
        string type,
        IReadOnlyDictionary<string, object?>? parameters = null,
        IReadOnlyDictionary<string, object?>? credentials = null,
        string? status = null)
    {
        Type = type;
        Parameters = parameters ?? EmptyMap;
        Credentials = new CloudCredentials(credentials);
        Status = status;
    }

    /// <summary>The delivery provider, as a raw wire string (an unknown provider round-trips untyped).</summary>
    public string Type { get; init; }

    /// <summary>Delivery locator keys (provider-specific).</summary>
    public IReadOnlyDictionary<string, object?> Parameters { get; init; }

    /// <summary>Secret keys; never rendered, never surfaced on read.</summary>
    public CloudCredentials Credentials { get; init; }

    /// <summary>Server-set delivery status on read; null on create.</summary>
    public string? Status { get; init; }

    /// <summary>Generic constructor keyed by a typed provider.</summary>
    public static OutputTarget Of(
        CloudProvider type,
        IReadOnlyDictionary<string, object?>? parameters = null,
        IReadOnlyDictionary<string, object?>? credentials = null) =>
        new(type.Wire(), parameters, credentials);

    /// <summary>Generic constructor keyed by a raw provider string (a forward-compat provider).</summary>
    public static OutputTarget Of(
        string type,
        IReadOnlyDictionary<string, object?>? parameters = null,
        IReadOnlyDictionary<string, object?>? credentials = null) =>
        new(type, parameters, credentials);

    /// <summary>
    /// The wire descriptor sent on create — <c>{ type, parameters, credentials }</c>, with <c>status</c>
    /// omitted (it is server-set and read-only).
    /// </summary>
    public IDictionary<string, object?> ToDescriptor() =>
        new Dictionary<string, object?>
        {
            ["type"] = Type,
            ["parameters"] = Parameters,
            ["credentials"] = Credentials.Values,
        };

    /// <summary>
    /// Hydrate from a <c>GET /jobs/{id}</c> <c>output_target[]</c> element. <c>type</c> / <c>status</c>
    /// stay raw strings (an unknown provider round-trips untyped); <c>credentials</c> are deliberately not
    /// surfaced.
    /// </summary>
    public static OutputTarget FromDict(IReadOnlyDictionary<string, object?> data) =>
        new(
            Data.String(data.GetValueOrDefault("type")),
            Data.Object(data.GetValueOrDefault("parameters")),
            null,
            Data.NullableString(data.GetValueOrDefault("status")));

    /// <summary>Human-readable form with credentials masked — safe to log.</summary>
    public override string ToString() =>
        $"OutputTarget(type={Type}, parameters={CloudInput.RenderParameters(Parameters)}, "
        + $"credentials={Redactor.Marker}, status={Status ?? "null"})";
}
