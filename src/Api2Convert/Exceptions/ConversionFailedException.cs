using System.Collections.Generic;
using Api2Convert.Models;

namespace Api2Convert.Exceptions;

/// <summary>
/// The job reached the <c>failed</c> (or <c>canceled</c>) status. The originating <see cref="Models.Job"/>
/// is attached so you can inspect its errors and warnings.
/// </summary>
public sealed class ConversionFailedException : Api2ConvertException
{
    public ConversionFailedException(Job job)
        : base(BuildMessage(job))
    {
        Job = job;
    }

    /// <summary>The failed job, including its <c>Errors</c> and <c>Warnings</c>.</summary>
    public Job Job { get; }

    /// <summary>The job's errors (may be empty if the API gave no detail).</summary>
    public IReadOnlyList<JobMessage> Errors => Job.Errors;

    private static string BuildMessage(Job job)
    {
        if (job.Errors.Count > 0)
        {
            JobMessage first = job.Errors[0];
            string code = first.Code is not null ? $" (code {first.Code})" : "";
            return $"Conversion failed: {first.Message}{code}";
        }

        string? info = job.Status.Info;
        return info is not null ? $"Conversion failed: {info}" : "Conversion failed.";
    }
}
