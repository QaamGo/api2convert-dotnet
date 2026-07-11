namespace Api2Convert;

/// <summary>
/// Optional controls for <see cref="Api2ConvertClient.StartConversionAsync"/>.
///
/// <para>Use object-initializer syntax:
/// <c>new AsyncOptions { Callback = "https://app.example.com/hook" }</c>.</para>
/// </summary>
public sealed class AsyncOptions
{
    /// <summary>Webhook URL notified when the job's status changes (also sets <c>notify_status</c>).</summary>
    public string? Callback { get; init; }

    /// <summary>Conversion category, when a target is ambiguous.</summary>
    public string? Category { get; init; }

    /// <summary>Filename to advertise for an uploaded local file.</summary>
    public string? Filename { get; init; }

    /// <summary>
    /// Protect the result with this password. The returned <see cref="Models.Job"/> is not a result
    /// wrapper, so a later download must supply the <c>X-Api2convert-Download-Password</c> header.
    /// </summary>
    public string? DownloadPassword { get; init; }
}
