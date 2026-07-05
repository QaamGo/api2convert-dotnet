using System.Collections.Generic;

namespace Api2Convert.Enums;

/// <summary>
/// The kinds of source an input file can be created from — the values of the API's input
/// <c>type</c> field. Provided as a typed reference for building input descriptors by hand, e.g.
/// <c>AddInputAsync(id, new Dictionary&lt;string, object?&gt; { ["type"] = InputType.Remote.Wire(),
/// ["source"] = "https://..." })</c>.
/// </summary>
public enum InputType
{
    /// <summary>A file uploaded directly to the per-job upload server.</summary>
    Upload,

    /// <summary>A file fetched by the API from a public URL.</summary>
    Remote,

    /// <summary>The output of a previous conversion in the same job.</summary>
    Output,

    /// <summary>A finished output of another job, by its id (job chaining).</summary>
    InputId,

    /// <summary>A file picked through the Google Drive picker.</summary>
    GdrivePicker,

    /// <summary>A small file embedded inline as base64.</summary>
    Base64,

    /// <summary>A file imported from cloud storage (S3, GCS, Azure, FTP, ...).</summary>
    Cloud,
}

/// <summary>Wire-format mapping for <see cref="InputType"/>.</summary>
public static class InputTypes
{
    private static readonly IReadOnlyDictionary<InputType, string> Wires = new Dictionary<InputType, string>
    {
        [InputType.Upload] = "upload",
        [InputType.Remote] = "remote",
        [InputType.Output] = "output",
        [InputType.InputId] = "input_id",
        [InputType.GdrivePicker] = "gdrive_picker",
        [InputType.Base64] = "base64",
        [InputType.Cloud] = "cloud",
    };

    /// <summary>The API's string value for this input type.</summary>
    public static string Wire(this InputType type) => Wires[type];

    /// <summary>Resolve a raw type string to a case, or null if unknown.</summary>
    public static InputType? FromWire(string? type)
    {
        if (type is not null)
        {
            foreach (KeyValuePair<InputType, string> entry in Wires)
            {
                if (entry.Value == type)
                {
                    return entry.Key;
                }
            }
        }

        return null;
    }
}
