using System.Collections.Generic;

namespace Api2Convert.Enums;

/// <summary>
/// The cloud storage providers the API can import inputs from and deliver outputs to — the values of
/// a cloud descriptor's <c>source</c> (input) / <c>type</c> (output) field.
///
/// <para>This is <strong>build-side vocabulary only</strong>: it types the input builder
/// (<see cref="Models.CloudInput"/>) and output-target serialization (<see cref="Models.OutputTarget"/>).
/// Read models keep <c>source</c> / <c>type</c> / <c>status</c> as raw strings, so an unknown provider
/// string returned by the server round-trips untyped and never throws — resolve tolerantly with
/// <see cref="CloudProviders.FromWire"/> (returns null on an unknown value), never a throwing parse.</para>
///
/// <para>An import factory (a <see cref="Models.CloudInput"/> named constructor) exists for
/// <see cref="AmazonS3"/>, <see cref="Azure"/>, <see cref="Ftp"/> and <see cref="GoogleCloud"/>.
/// <see cref="Gdrive"/> and <see cref="Youtube"/> are <strong>output-only</strong> (they validate as an
/// output <c>type</c> but have no downloader); Google Drive <em>input</em> uses the separate
/// <c>gdrive_picker</c> input type via the generic <c>AddInputAsync</c> raw-map path.</para>
/// </summary>
public enum CloudProvider
{
    /// <summary>Amazon S3.</summary>
    AmazonS3,

    /// <summary>Azure Blob Storage.</summary>
    Azure,

    /// <summary>An FTP server.</summary>
    Ftp,

    /// <summary>Google Drive (output-only).</summary>
    Gdrive,

    /// <summary>Google Cloud Storage.</summary>
    GoogleCloud,

    /// <summary>YouTube (output-only).</summary>
    Youtube,
}

/// <summary>Wire-format mapping for <see cref="CloudProvider"/>.</summary>
public static class CloudProviders
{
    private static readonly IReadOnlyDictionary<CloudProvider, string> Wires = new Dictionary<CloudProvider, string>
    {
        [CloudProvider.AmazonS3] = "amazons3",
        [CloudProvider.Azure] = "azure",
        [CloudProvider.Ftp] = "ftp",
        [CloudProvider.Gdrive] = "gdrive",
        [CloudProvider.GoogleCloud] = "googlecloud",
        [CloudProvider.Youtube] = "youtube",
    };

    /// <summary>The API's string value for this provider.</summary>
    public static string Wire(this CloudProvider provider) => Wires[provider];

    /// <summary>
    /// Resolve a raw provider string to a case, or null if unknown. Tolerant on purpose: an unknown
    /// provider from the server hydrates untyped rather than throwing.
    /// </summary>
    public static CloudProvider? FromWire(string? provider)
    {
        if (provider is not null)
        {
            foreach (KeyValuePair<CloudProvider, string> entry in Wires)
            {
                if (entry.Value == provider)
                {
                    return entry.Key;
                }
            }
        }

        return null;
    }
}
