using System.Collections.Generic;

namespace Api2Convert.Support;

/// <summary>
/// Credential redaction for cloud connectors.
///
/// <para>Cloud <c>credentials</c> ride in the plaintext request body, so they must never surface where
/// a value object or an SDK-emitted string could leak them. This helper centralizes the masks the
/// contract mandates:</para>
/// <list type="bullet">
///   <item>the <strong>whole <c>credentials</c> object</strong> collapses to <see cref="Marker"/> on
///     every object-inspection path (<c>ToString</c>) — realized by <see cref="Models.CloudCredentials"/>;</item>
///   <item>any <c>parameters</c> leaf whose key contains a sensitive token
///     (<see cref="IsSensitiveKey"/>, case-insensitive substring) collapses to <see cref="Marker"/>;</item>
///   <item>the decoded error body is deep-walked (<see cref="MaskSensitive"/>) as belt-and-suspenders —
///     the API only ever echoes field <em>names</em>, never a credential <em>value</em>, but a future
///     server/proxy change must not be able to leak one.</item>
/// </list>
///
/// <para>Internal helper, not part of the public API.</para>
/// </summary>
internal static class Redactor
{
    /// <summary>The fixed, fleet-wide redaction marker (D9).</summary>
    public const string Marker = "[REDACTED]";

    private static readonly IReadOnlyDictionary<string, object?> EmptyObject = new Dictionary<string, object?>(0);

    /// <summary>
    /// Case-insensitive substrings that mark a key as carrying a secret. A key containing any of these
    /// has its whole value masked.
    /// </summary>
    private static readonly string[] SensitiveSubstrings =
    {
        "token", "password", "passwd", "secret", "key", "keyfile",
        "credential", "passphrase", "sas", "sig", "signature",
    };

    /// <summary>Whether a key name marks its value as sensitive (case-insensitive substring match).</summary>
    public static bool IsSensitiveKey(string? key)
    {
        if (key is null)
        {
            return false;
        }

        string lower = key.ToLowerInvariant();
        foreach (string needle in SensitiveSubstrings)
        {
            if (lower.Contains(needle))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Return a deep copy of <paramref name="value"/> with every sensitive-keyed leaf replaced by
    /// <see cref="Marker"/>. Recurses through maps and lists; a sensitive key masks its whole value
    /// (even a nested object). Non-map/list values pass through unchanged.
    ///
    /// <para>Used both for <c>parameters</c> rendering and the decoded error-body deep-walk (a dotted /
    /// nested echoed key such as <c>input.0.credentials.secretaccesskey</c>).</para>
    /// </summary>
    public static object? MaskSensitive(object? value)
    {
        switch (value)
        {
            case IReadOnlyDictionary<string, object?> map:
                var maskedMap = new Dictionary<string, object?>(map.Count);
                foreach (KeyValuePair<string, object?> entry in map)
                {
                    maskedMap[entry.Key] = IsSensitiveKey(entry.Key) ? Marker : MaskSensitive(entry.Value);
                }

                return maskedMap;

            case IReadOnlyList<object?> list:
                var maskedList = new List<object?>(list.Count);
                foreach (object? item in list)
                {
                    maskedList.Add(MaskSensitive(item));
                }

                return maskedList;

            default:
                return value;
        }
    }

    /// <summary>Mask sensitive leaves of a <c>parameters</c> map (see <see cref="MaskSensitive"/>).</summary>
    public static IReadOnlyDictionary<string, object?> MaskParameters(IReadOnlyDictionary<string, object?> parameters) =>
        MaskSensitive(parameters) as IReadOnlyDictionary<string, object?> ?? EmptyObject;

    /// <summary>
    /// Deep-walk a decoded error body and mask the value of every sensitive key (belt-and-suspenders).
    /// </summary>
    public static IReadOnlyDictionary<string, object?> RedactBody(IReadOnlyDictionary<string, object?> body) =>
        MaskSensitive(body) as IReadOnlyDictionary<string, object?> ?? EmptyObject;
}
