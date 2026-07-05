using System.Collections.Generic;
using Api2Convert.Enums;
using Api2Convert.Support;

namespace Api2Convert.Models;

/// <summary>
/// A conversion job — the central API2Convert resource.
///
/// <para>Returned by every job operation. <see cref="Server"/> and <see cref="Token"/> are needed to
/// upload local files; <see cref="Output"/> holds the produced files once <see cref="IsCompleted"/>.
/// <see cref="Raw"/> keeps the full decoded response for fields not surfaced as typed properties.</para>
/// </summary>
public sealed record Job(
    string Id,
    Status Status,
    string? Token,
    string? Server,
    string? Callback,
    IReadOnlyList<Conversion> Conversion,
    IReadOnlyList<InputFile> Input,
    IReadOnlyList<OutputFile> Output,
    IReadOnlyList<JobMessage> Errors,
    IReadOnlyList<JobMessage> Warnings,
    IReadOnlyDictionary<string, object?> Raw)
{
    public static Job FromDict(IReadOnlyDictionary<string, object?> data) =>
        new(
            Data.String(data.GetValueOrDefault("id")),
            Status.FromDict(Data.Object(data.GetValueOrDefault("status"))),
            Data.NullableString(data.GetValueOrDefault("token")),
            Data.NullableString(data.GetValueOrDefault("server")),
            Data.NullableString(data.GetValueOrDefault("callback")),
            Data.MapObjects(data.GetValueOrDefault("conversion"), Models.Conversion.FromDict),
            Data.MapObjects(data.GetValueOrDefault("input"), InputFile.FromDict),
            Data.MapObjects(data.GetValueOrDefault("output"), OutputFile.FromDict),
            Data.MapObjects(data.GetValueOrDefault("errors"), JobMessage.FromDict),
            Data.MapObjects(data.GetValueOrDefault("warnings"), JobMessage.FromDict),
            data);

    /// <summary>The job finished successfully and produced its output.</summary>
    public bool IsCompleted => Status.Code == JobStatus.Completed.Wire();

    /// <summary>The job failed server-side — terminal.</summary>
    public bool IsFailed => Status.Code == JobStatus.Failed.Wire();

    /// <summary>The job was canceled server-side — terminal, and produced no output.</summary>
    public bool IsCanceled => Status.Code == JobStatus.Canceled.Wire();

    /// <summary>Finished (completed, failed or canceled) and will not change further.</summary>
    public bool IsTerminal => JobStatuses.IsTerminalCode(Status.Code);
}
