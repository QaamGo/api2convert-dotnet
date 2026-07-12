using System.Collections.Generic;
using Api2Convert.Models;

namespace Api2Convert;

/// <summary>
/// Optional, less-common controls for <see cref="Api2ConvertClient.ConvertAsync"/>. Kept separate
/// from the open-ended conversion-options map so SDK controls can never collide with an API option
/// key.
///
/// <para>Use object-initializer syntax:
/// <c>new ConvertOptions { Category = "image", DownloadPassword = "hunter2" }</c>.</para>
/// </summary>
public sealed class ConvertOptions
{
    /// <summary>Conversion category, when a target is ambiguous.</summary>
    public string? Category { get; init; }

    /// <summary>Override the poll timeout (seconds).</summary>
    public int? Timeout { get; init; }

    /// <summary>Which output file the result exposes (default 0).</summary>
    public int? OutputIndex { get; init; }

    /// <summary>Filename to advertise for an uploaded local file.</summary>
    public string? Filename { get; init; }

    /// <summary>Protect the result with this password; remembered and sent automatically on download.</summary>
    public string? DownloadPassword { get; init; }

    /// <summary>
    /// Cloud delivery targets attached to the conversion's <c>output_target</c> (never merged into the
    /// options map). When set, the conversion delivers straight to your storage and produces no local
    /// output, so the returned result's job is complete but exposes no downloadable output.
    /// </summary>
    public IReadOnlyList<OutputTarget>? OutputTargets { get; init; }
}
