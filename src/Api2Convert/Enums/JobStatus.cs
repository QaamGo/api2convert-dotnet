using System.Collections.Generic;

namespace Api2Convert.Enums;

/// <summary>
/// Well-known job status codes (the <c>status.code</c> field).
///
/// <para>The API may introduce further codes; treat any code not listed here as non-terminal. Use
/// <see cref="JobStatuses.IsTerminal"/> on a case, or <see cref="JobStatuses.IsTerminalCode"/> for a
/// raw status string, rather than comparing strings by hand.</para>
/// </summary>
public enum JobStatus
{
    Created,
    Incomplete,
    Downloading,
    Queued,
    Processing,
    Completed,
    Failed,
    Canceled,
}

/// <summary>Wire-format mapping and terminal-state helpers for <see cref="JobStatus"/>.</summary>
public static class JobStatuses
{
    private static readonly IReadOnlyDictionary<JobStatus, string> Wires = new Dictionary<JobStatus, string>
    {
        [JobStatus.Created] = "created",
        [JobStatus.Incomplete] = "incomplete",
        [JobStatus.Downloading] = "downloading",
        [JobStatus.Queued] = "queued",
        [JobStatus.Processing] = "processing",
        [JobStatus.Completed] = "completed",
        [JobStatus.Failed] = "failed",
        [JobStatus.Canceled] = "canceled",
    };

    /// <summary>The API's string value for this status.</summary>
    public static string Wire(this JobStatus status) => Wires[status];

    /// <summary>Resolve a raw status code to a case, or null if it is not a known value.</summary>
    public static JobStatus? FromWire(string? code)
    {
        if (code is not null)
        {
            foreach (KeyValuePair<JobStatus, string> entry in Wires)
            {
                if (entry.Value == code)
                {
                    return entry.Key;
                }
            }
        }

        return null;
    }

    /// <summary>A job in a terminal state is finished and will not change further.</summary>
    public static bool IsTerminal(this JobStatus status) =>
        status is JobStatus.Completed or JobStatus.Failed or JobStatus.Canceled;

    /// <summary>Is the given raw status code a terminal one? Unknown codes are non-terminal.</summary>
    public static bool IsTerminalCode(string? code)
    {
        JobStatus? status = FromWire(code);
        return status is not null && status.Value.IsTerminal();
    }
}
